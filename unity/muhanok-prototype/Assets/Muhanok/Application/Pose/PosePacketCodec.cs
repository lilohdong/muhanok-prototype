#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace Muhanok.Application.Pose
{
    /// §7 패킷(한 줄 JSON) ↔ PoseFrame. 스키마가 고정이라 범용 JSON 라이브러리 없이 전용 파서로 처리한다.
    /// 의존성 0, 어떤 스레드에서든 안전(상태 없음).
    public static class PosePacketCodec
    {
        public const int ProtocolVersion = 1;

        public enum Result { Ok, Malformed, VersionMismatch }

        public static Result TryDecode(string line, out PoseFrame? frame)
        {
            frame = null;
            try
            {
                var p = new Parser(line);
                return p.ParsePacket(out frame);
            }
            catch (FormatException)
            {
                return Result.Malformed;
            }
        }

        public static string Encode(PoseFrame frame)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"v\":").Append(ProtocolVersion);
            sb.Append(",\"seq\":").Append(frame.Seq.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"t\":").Append(frame.Time.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(",\"ok\":").Append(frame.Ok ? "true" : "false");
            if (frame.Ok)
            {
                sb.Append(",\"lm\":{");
                var first = true;
                foreach (var idx in LandmarkIndex.Sent)
                {
                    if (!frame.TryGet(idx, out var lm)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(idx).Append("\":[")
                      .Append(lm.X.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                      .Append(lm.Y.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                      .Append(lm.Visibility.ToString("F3", CultureInfo.InvariantCulture)).Append(']');
                }
                sb.Append('}');
            }
            sb.Append('}');
            return sb.ToString();
        }

        private struct Parser
        {
            private readonly string s;
            private int i;

            public Parser(string source)
            {
                s = source;
                i = 0;
            }

            public Result ParsePacket(out PoseFrame? frame)
            {
                frame = null;
                long seq = -1;
                double t = 0;
                var ok = false;
                var version = -1;
                Landmark[]? slots = null;
                bool[]? present = null;

                SkipWs();
                Expect('{');
                while (true)
                {
                    SkipWs();
                    if (Peek() == '}') { i++; break; }
                    var key = ParseString();
                    SkipWs();
                    Expect(':');
                    SkipWs();
                    switch (key)
                    {
                        case "v": version = (int)ParseNumber(); break;
                        case "seq": seq = (long)ParseNumber(); break;
                        case "t": t = ParseNumber(); break;
                        case "ok": ok = ParseBool(); break;
                        case "lm": ParseLandmarks(out slots, out present); break;
                        default: SkipValue(); break;
                    }
                    SkipWs();
                    if (Peek() == ',') { i++; continue; }
                    if (Peek() == '}') { i++; break; }
                    throw Fail();
                }

                if (version != ProtocolVersion) return Result.VersionMismatch;
                if (seq < 0) return Result.Malformed;
                frame = ok && slots != null
                    ? new PoseFrame(seq, t, true, slots, present)
                    : PoseFrame.Lost(seq, t);
                return Result.Ok;
            }

            private void ParseLandmarks(out Landmark[]? slots, out bool[]? present)
            {
                if (Peek() == 'n') { SkipValue(); slots = null; present = null; return; }
                slots = new Landmark[LandmarkIndex.SlotCount];
                present = new bool[LandmarkIndex.SlotCount];
                Expect('{');
                while (true)
                {
                    SkipWs();
                    if (Peek() == '}') { i++; break; }
                    var key = ParseString();
                    SkipWs();
                    Expect(':');
                    SkipWs();
                    Expect('[');
                    SkipWs(); var x = (float)ParseNumber(); SkipWs(); Expect(',');
                    SkipWs(); var y = (float)ParseNumber(); SkipWs(); Expect(',');
                    SkipWs(); var vis = (float)ParseNumber(); SkipWs();
                    Expect(']');

                    if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    {
                        var slot = LandmarkIndex.SlotOf(idx);
                        if (slot >= 0)
                        {
                            slots[slot] = new Landmark(x, y, vis);
                            present[slot] = true;
                        }
                    }
                    SkipWs();
                    if (Peek() == ',') { i++; continue; }
                    if (Peek() == '}') { i++; break; }
                    throw Fail();
                }
            }

            private char Peek() => i < s.Length ? s[i] : '\0';

            private void SkipWs()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
            }

            private void Expect(char c)
            {
                if (Peek() != c) throw Fail();
                i++;
            }

            private FormatException Fail() => new FormatException("bad pose packet at " + i);

            private string ParseString()
            {
                Expect('"');
                var start = i;
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\') i++;
                    i++;
                }
                if (i >= s.Length) throw Fail();
                var result = s.Substring(start, i - start);
                i++;
                return result;
            }

            private double ParseNumber()
            {
                var start = i;
                while (i < s.Length)
                {
                    var c = s[i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') i++;
                    else break;
                }
                if (start == i) throw Fail();
                if (!double.TryParse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw Fail();
                return v;
            }

            private bool ParseBool()
            {
                if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                throw Fail();
            }

            /// 모르는 키의 값은 구조만 따라가며 건너뛴다. 프로토콜에 필드가 추가돼도 구버전 파서가 죽지 않게.
            private void SkipValue()
            {
                var c = Peek();
                if (c == '"') { ParseString(); return; }
                if (c == '{' || c == '[')
                {
                    var depth = 0;
                    while (i < s.Length)
                    {
                        c = s[i];
                        if (c == '"') { ParseString(); continue; }
                        if (c == '{' || c == '[') depth++;
                        else if (c == '}' || c == ']') { depth--; if (depth == 0) { i++; return; } }
                        i++;
                    }
                    throw Fail();
                }
                while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']') i++;
            }
        }
    }
}
