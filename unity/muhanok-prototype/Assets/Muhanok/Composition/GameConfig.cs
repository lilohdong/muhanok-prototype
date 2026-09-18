#nullable enable
using Muhanok.Application;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using UnityEngine;

namespace Muhanok.Composition
{
    public enum InputSourceKind { Keyboard, Pose }

    /// 합성 루트가 읽는 설정 한 장. 씬에서 GameLifetimeScope에 꽂는다. 없으면 이 클래스의 기본값으로 돈다.
    /// 값의 근거는 CLAUDE.md §14.2.
    [CreateAssetMenu(menuName = "Muhanok/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Input")]
        [Tooltip("Keyboard: Space/W/S/A/D.  Pose: UDP 127.0.0.1:udpPort 에서 포즈 수신")]
        public InputSourceKind inputSource = InputSourceKind.Keyboard;
        public int udpPort = 52100;
        [Range(0.01f, 1f)] public float latencySmoothing = 0.1f;

        [Header("Profiles (비어 있으면 기본값으로 생성)")]
        public TuningProfile? tuning;
        public PresentationProfile? presentation;

        [Header("Run (§14.2)")]
        public float speedStart = 6f;
        public float speedMax = 14f;
        public float speedGainPerMeter = 0.02f;
        public float chaserGapStart = 10f;
        public float chaserGapMax = 12f;
        public float stumblePenalty = 4f;
        public float gapRecoveryPerSecond = 0.5f;
        public int coinScore = 10;
        public float postureDuration = 0.6f;

        [Header("Track")]
        public float spawnAheadDistance = 120f;
        public float retireBehindDistance = 20f;

        [Header("Segment generator")]
        public int seed = 1;
        public float segmentLength = 40f;
        public int warmupSegments = 1;
        public float firstObstacleOffset = 8f;
        public float obstacleSpacing = 10f;
        public int coinsPerRow = 3;
        public float coinSpacing = 1.5f;
        public float coinRowOffset = 3f;

        public RunTuning ToRunTuning() => new RunTuning(
            speedStart, speedMax, speedGainPerMeter,
            chaserGapStart, chaserGapMax, stumblePenalty, gapRecoveryPerSecond,
            coinScore, postureDuration);

        public TrackSettings ToTrackSettings() => new TrackSettings(spawnAheadDistance, retireBehindDistance);

        public SegmentGeneratorSettings ToGeneratorSettings() => new SegmentGeneratorSettings(
            seed, segmentLength, warmupSegments, firstObstacleOffset, obstacleSpacing,
            coinsPerRow, coinSpacing, coinRowOffset);
    }
}
