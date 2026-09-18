#nullable enable
namespace Muhanok.Domain
{
    /// 한 판의 진행 규칙에 들어가는 숫자. 전부 바깥(ScriptableObject)에서 주입된다. 코드에 숫자를 박지 않는다.
    public sealed class RunTuning
    {
        public float SpeedStart { get; }
        public float SpeedMax { get; }
        public float SpeedGainPerMeter { get; }
        public float ChaserGapStart { get; }
        public float ChaserGapMax { get; }
        public float StumblePenalty { get; }
        public float GapRecoveryPerSecond { get; }
        public int CoinScore { get; }
        /// Jump/KneeRaise/Duck 이벤트 뒤 자세가 유지되는 시간(초). 키보드·포즈 양쪽이 이벤트만 내면 되게 하는 장치.
        public float PostureDuration { get; }

        public RunTuning(
            float speedStart,
            float speedMax,
            float speedGainPerMeter,
            float chaserGapStart,
            float chaserGapMax,
            float stumblePenalty,
            float gapRecoveryPerSecond,
            int coinScore,
            float postureDuration)
        {
            SpeedStart = speedStart;
            SpeedMax = speedMax;
            SpeedGainPerMeter = speedGainPerMeter;
            ChaserGapStart = chaserGapStart;
            ChaserGapMax = chaserGapMax;
            StumblePenalty = stumblePenalty;
            GapRecoveryPerSecond = gapRecoveryPerSecond;
            CoinScore = coinScore;
            PostureDuration = postureDuration;
        }
    }
}
