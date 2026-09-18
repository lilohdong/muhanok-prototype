#nullable enable
using System;

namespace Muhanok.Application.Pose
{
    /// 고정 크기 링 버퍼의 중앙값. 평균이 아니라 중앙값을 쓰는 이유: 튀는 프레임 하나에 기준이 흔들리지 않게.
    public sealed class MedianBaseline
    {
        private readonly float[] ring;
        private readonly float[] scratch;
        private int count;
        private int head;

        public MedianBaseline(int capacity)
        {
            ring = new float[capacity];
            scratch = new float[capacity];
        }

        public int Count => count;

        public void Add(float value)
        {
            ring[head] = value;
            head = (head + 1) % ring.Length;
            if (count < ring.Length) count++;
        }

        public float Median
        {
            get
            {
                if (count == 0) return 0f;
                Array.Copy(ring, scratch, count);
                Array.Sort(scratch, 0, count);
                return count % 2 == 1
                    ? scratch[count / 2]
                    : (scratch[count / 2 - 1] + scratch[count / 2]) * 0.5f;
            }
        }

        public void Clear()
        {
            count = 0;
            head = 0;
        }
    }

    /// 안정화 3종 세트(§8.4)를 한 채널에 묶은 것: N프레임 확인 + 히스테리시스 + 쿨다운.
    /// 입력은 "측정값 / 진입 임계값" 비율. 1 이상이면 조건 충족.
    public sealed class GestureChannel
    {
        private readonly int confirmFrames;
        private readonly float releaseRatio;
        private readonly double cooldownSeconds;

        private int consecutive;
        private bool active;
        private double lastFiredAt = double.NegativeInfinity;

        public bool Active => active;

        public GestureChannel(int confirmFrames, float releaseRatio, double cooldownSeconds)
        {
            this.confirmFrames = confirmFrames;
            this.releaseRatio = releaseRatio;
            this.cooldownSeconds = cooldownSeconds;
        }

        /// 이 프레임에 동작이 "인정"되면 true. 활성 상태가 유지되는 동안은 다시 인정하지 않는다.
        public bool Update(float ratio, double time)
        {
            if (active)
            {
                if (ratio < releaseRatio)
                {
                    active = false;
                    consecutive = 0;
                }
                return false;
            }

            consecutive = ratio >= 1f ? consecutive + 1 : 0;
            if (consecutive < confirmFrames) return false;

            active = true;
            consecutive = 0;
            if (time - lastFiredAt < cooldownSeconds) return false;
            lastFiredAt = time;
            return true;
        }

        public void Reset()
        {
            active = false;
            consecutive = 0;
        }
    }
}
