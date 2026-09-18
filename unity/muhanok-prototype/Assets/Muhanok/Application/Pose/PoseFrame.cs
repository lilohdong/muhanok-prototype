#nullable enable
using System;

namespace Muhanok.Application.Pose
{
    /// MediaPipe 랜드마크 인덱스. Python이 보내는 13개(§7)만 슬롯을 갖는다.
    public static class LandmarkIndex
    {
        public const int Nose = 0;
        public const int LeftShoulder = 11;
        public const int RightShoulder = 12;
        public const int LeftElbow = 13;
        public const int RightElbow = 14;
        public const int LeftWrist = 15;
        public const int RightWrist = 16;
        public const int LeftHip = 23;
        public const int RightHip = 24;
        public const int LeftKnee = 25;
        public const int RightKnee = 26;
        public const int LeftAnkle = 27;
        public const int RightAnkle = 28;

        public static readonly int[] Sent =
            { Nose, LeftShoulder, RightShoulder, LeftElbow, RightElbow, LeftWrist, RightWrist,
              LeftHip, RightHip, LeftKnee, RightKnee, LeftAnkle, RightAnkle };

        public const int SlotCount = 13;

        /// MediaPipe 인덱스 → 슬롯. 보내지 않는 인덱스는 -1.
        public static int SlotOf(int mediaPipeIndex)
        {
            for (var i = 0; i < Sent.Length; i++)
                if (Sent[i] == mediaPipeIndex) return i;
            return -1;
        }
    }

    /// 정규화 좌표(0~1). MediaPipe 규약대로 y는 아래로 갈수록 커진다. 부호를 뒤집지 않는다.
    public readonly struct Landmark
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Visibility;

        public Landmark(float x, float y, float visibility)
        {
            X = x;
            Y = y;
            Visibility = visibility;
        }
    }

    /// 한 프레임의 포즈 입력. 순수 데이터. UDP에서 왔든 녹화 파일에서 왔든 같다.
    public sealed class PoseFrame
    {
        public long Seq { get; }
        /// 송신 측 Unix 시각(초). 쿨다운·베이스라인 창은 이 시간축을 쓴다.
        public double Time { get; }
        public bool Ok { get; }

        private readonly Landmark[] slots;
        private readonly bool[] present;

        public PoseFrame(long seq, double time, bool ok, Landmark[]? slots, bool[]? present)
        {
            Seq = seq;
            Time = time;
            Ok = ok;
            this.slots = slots ?? new Landmark[LandmarkIndex.SlotCount];
            this.present = present ?? new bool[LandmarkIndex.SlotCount];
        }

        public static PoseFrame Lost(long seq, double time) => new PoseFrame(seq, time, false, null, null);

        public bool Has(int mediaPipeIndex)
        {
            var slot = LandmarkIndex.SlotOf(mediaPipeIndex);
            return slot >= 0 && present[slot];
        }

        public Landmark Get(int mediaPipeIndex)
        {
            var slot = LandmarkIndex.SlotOf(mediaPipeIndex);
            if (slot < 0 || !present[slot])
                throw new ArgumentOutOfRangeException(nameof(mediaPipeIndex), "landmark not present: " + mediaPipeIndex);
            return slots[slot];
        }

        public bool TryGet(int mediaPipeIndex, out Landmark landmark)
        {
            var slot = LandmarkIndex.SlotOf(mediaPipeIndex);
            if (slot < 0 || !present[slot])
            {
                landmark = default;
                return false;
            }
            landmark = slots[slot];
            return true;
        }
    }

    /// 테스트·재생용 프레임 조립기.
    public sealed class PoseFrameBuilder
    {
        private readonly Landmark[] slots = new Landmark[LandmarkIndex.SlotCount];
        private readonly bool[] present = new bool[LandmarkIndex.SlotCount];

        public PoseFrameBuilder Set(int mediaPipeIndex, float x, float y, float visibility)
        {
            var slot = LandmarkIndex.SlotOf(mediaPipeIndex);
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(mediaPipeIndex));
            slots[slot] = new Landmark(x, y, visibility);
            present[slot] = true;
            return this;
        }

        public PoseFrame Build(long seq, double time)
        {
            var s = new Landmark[LandmarkIndex.SlotCount];
            var p = new bool[LandmarkIndex.SlotCount];
            Array.Copy(slots, s, s.Length);
            Array.Copy(present, p, p.Length);
            return new PoseFrame(seq, time, true, s, p);
        }
    }
}
