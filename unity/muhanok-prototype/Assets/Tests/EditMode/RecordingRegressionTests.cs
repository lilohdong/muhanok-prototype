#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Muhanok.Application.Pose;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    /// 녹화 파일 회귀 테스트(§12-2). recordings/<action>_x<N>.ndjson 을 먹여 <action>이 정확히 N회 검출되는지 본다.
    /// 예: recordings/jump_x10.ndjson → Jump 10회, 다른 동작 0회.
    /// 임계값을 바꿀 때마다 이 테스트를 돌린다 — 한 동작을 고치다 다른 동작을 망가뜨리는 사고를 잡는다.
    public sealed class RecordingRegressionTests
    {
        private static readonly Regex Name = new Regex(@"^(jump|kneeraise|armraise|duck|stepleft|stepright)_x(\d+)\.ndjson$", RegexOptions.IgnoreCase);

        private static string RecordingsDir =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "..", "recordings"));

        public static IEnumerable<TestCaseData> Cases()
        {
            var any = false;
            if (Directory.Exists(RecordingsDir))
            {
                foreach (var path in Directory.GetFiles(RecordingsDir, "*.ndjson"))
                {
                    var m = Name.Match(Path.GetFileName(path));
                    if (!m.Success) continue;
                    var action = ParseAction(m.Groups[1].Value);
                    var expected = int.Parse(m.Groups[2].Value);
                    any = true;
                    yield return new TestCaseData(path, action, expected).SetName("Recording_" + Path.GetFileNameWithoutExtension(path));
                }
            }
            // 빈 TestCaseSource는 NUnit이 실패로 본다. 녹화가 없으면 Ignore 케이스 하나를 낸다.
            if (!any)
                yield return new TestCaseData("", PlayerAction.Jump, 0).SetName("Recording_none").Ignore("no *_xN.ndjson in " + RecordingsDir);
        }

        private static PlayerAction ParseAction(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "jump": return PlayerAction.Jump;
                case "kneeraise":
                case "armraise": return PlayerAction.KneeRaise;
                case "duck": return PlayerAction.Duck;
                case "stepleft": return PlayerAction.StepLeft;
                default: return PlayerAction.StepRight;
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void Recording_DetectsExpectedCount(string path, PlayerAction action, int expected)
        {
            var detector = new PostureDetector(PostureDetectorTests.UpperBodyDefaults());
            var counts = new Dictionary<PlayerAction, int>();
            var scratch = new PlayerAction[8];
            var frames = 0;
            var bad = 0;

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (PosePacketCodec.TryDecode(line, out var frame) != PosePacketCodec.Result.Ok || frame == null)
                {
                    bad++;
                    continue;
                }
                frames++;
                var n = detector.Step(frame, scratch);
                for (var i = 0; i < n; i++)
                    counts[scratch[i]] = counts.TryGetValue(scratch[i], out var c) ? c + 1 : 1;
            }

            Assert.That(frames, Is.GreaterThan(0), "no decodable frames");
            Assert.That(bad, Is.EqualTo(0), "malformed lines");
            counts.TryGetValue(action, out var got);
            Assert.That(got, Is.EqualTo(expected), $"{action} count in {Path.GetFileName(path)}: {Describe(counts)}");

            // 사이드 스텝 녹화는 되돌아오는 반대 방향 스텝이 같이 찍힌다. 그 외 동작은 다른 동작이 나오면 안 된다.
            var isStep = action == PlayerAction.StepLeft || action == PlayerAction.StepRight;
            foreach (var kv in counts)
            {
                if (kv.Key == action) continue;
                var opposite = isStep && (kv.Key == PlayerAction.StepLeft || kv.Key == PlayerAction.StepRight);
                if (opposite) continue;
                Assert.That(kv.Value, Is.EqualTo(0), $"false positive {kv.Key} in {Path.GetFileName(path)}: {Describe(counts)}");
            }
        }

        private static string Describe(Dictionary<PlayerAction, int> counts)
        {
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(", ", parts);
        }
    }
}
