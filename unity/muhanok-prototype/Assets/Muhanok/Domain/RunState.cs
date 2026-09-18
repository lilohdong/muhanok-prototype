#nullable enable
using System;

namespace Muhanok.Domain
{
    /// 한 판의 진행 상태. 불변. 변경은 새 인스턴스를 돌려준다.
    public sealed class RunState
    {
        public float DistanceTravelled { get; }
        public float ForwardSpeed { get; }
        public Lane CurrentLane { get; }
        public PostureState Posture { get; }
        public float ChaserGap { get; }   // 교도관과의 거리. 0 이하면 종료
        public int Coins { get; }
        public int Score { get; }
        public bool IsOver { get; }
        public int Stumbles { get; }

        public RunState(
            float distanceTravelled,
            float forwardSpeed,
            Lane currentLane,
            PostureState posture,
            float chaserGap,
            int coins,
            int score,
            bool isOver,
            int stumbles)
        {
            DistanceTravelled = distanceTravelled;
            ForwardSpeed = forwardSpeed;
            CurrentLane = currentLane;
            Posture = posture;
            ChaserGap = chaserGap;
            Coins = coins;
            Score = score;
            IsOver = isOver;
            Stumbles = stumbles;
        }

        public static RunState Initial(RunTuning tuning) => new RunState(
            distanceTravelled: 0f,
            forwardSpeed: tuning.SpeedStart,
            currentLane: Lane.Center,
            posture: PostureState.Grounded,
            chaserGap: tuning.ChaserGapStart,
            coins: 0,
            score: 0,
            isOver: false,
            stumbles: 0);

        public RunState With(
            float? distanceTravelled = null,
            float? forwardSpeed = null,
            Lane? currentLane = null,
            PostureState? posture = null,
            float? chaserGap = null,
            int? coins = null,
            int? score = null,
            bool? isOver = null,
            int? stumbles = null)
        {
            return new RunState(
                distanceTravelled ?? DistanceTravelled,
                forwardSpeed ?? ForwardSpeed,
                currentLane ?? CurrentLane,
                posture ?? Posture,
                chaserGap ?? ChaserGap,
                coins ?? Coins,
                score ?? Score,
                isOver ?? IsOver,
                stumbles ?? Stumbles);
        }
    }

    /// RunState를 굴리는 순수 규칙 모음. 시간·랜덤·IO 없음.
    public static class RunRules
    {
        public static int ComputeScore(float distance, int coins, RunTuning t)
            => (int)Math.Floor(distance) + coins * t.CoinScore;

        /// 시간 경과: 속도 증가, 전진, 교도관 거리 회복.
        public static RunState Advance(RunState s, float deltaSeconds, RunTuning t)
        {
            if (s.IsOver || deltaSeconds <= 0f) return s;

            var speed = Math.Min(t.SpeedMax, t.SpeedStart + t.SpeedGainPerMeter * s.DistanceTravelled);
            var distance = s.DistanceTravelled + speed * deltaSeconds;
            var gap = Math.Min(t.ChaserGapMax, s.ChaserGap + t.GapRecoveryPerSecond * deltaSeconds);

            return s.With(
                distanceTravelled: distance,
                forwardSpeed: speed,
                chaserGap: gap,
                score: ComputeScore(distance, s.Coins, t));
        }

        public static RunState SetPosture(RunState s, PostureState posture)
            => s.IsOver ? s : s.With(posture: posture);

        public static RunState ShiftLane(RunState s, int direction)
            => s.IsOver ? s : s.With(currentLane: LaneRules.Shift(s.CurrentLane, direction));

        /// 장애물에 부딪힘. 교도관이 가까워지고, 0 이하가 되면 잡힌 것이다.
        public static RunState Stumble(RunState s, RunTuning t)
        {
            if (s.IsOver) return s;
            var gap = s.ChaserGap - t.StumblePenalty;
            return s.With(
                chaserGap: gap,
                stumbles: s.Stumbles + 1,
                isOver: gap <= 0f);
        }

        public static RunState CollectCoin(RunState s, RunTuning t)
        {
            if (s.IsOver) return s;
            var coins = s.Coins + 1;
            return s.With(coins: coins, score: ComputeScore(s.DistanceTravelled, coins, t));
        }
    }
}
