#nullable enable
using System;
using System.Collections.Generic;
using Muhanok.Application.Ports;
using Muhanok.Domain;

namespace Muhanok.Infrastructure
{
    public sealed class SegmentGeneratorSettings
    {
        public int Seed { get; }
        public float SegmentLength { get; }
        /// 처음 몇 구간은 장애물 없이 코인만. 몸을 풀 시간.
        public int WarmupSegments { get; }
        public float FirstObstacleOffset { get; }
        public float ObstacleSpacing { get; }
        public int CoinsPerRow { get; }
        public float CoinSpacing { get; }
        /// 장애물 뒤 이만큼 떨어진 곳부터 코인 줄이 시작된다.
        public float CoinRowOffset { get; }

        public SegmentGeneratorSettings(
            int seed,
            float segmentLength,
            int warmupSegments,
            float firstObstacleOffset,
            float obstacleSpacing,
            int coinsPerRow,
            float coinSpacing,
            float coinRowOffset)
        {
            Seed = seed;
            SegmentLength = segmentLength;
            WarmupSegments = warmupSegments;
            FirstObstacleOffset = firstObstacleOffset;
            ObstacleSpacing = obstacleSpacing;
            CoinsPerRow = coinsPerRow;
            CoinSpacing = coinSpacing;
            CoinRowOffset = coinRowOffset;
        }
    }

    /// 시드 + 구간 번호로 결정적으로 배치도를 만든다. 같은 시드면 같은 트랙 — 재현 가능한 버그 리포트를 위해.
    public sealed class SeededSegmentLayoutGenerator : ISegmentLayoutGenerator
    {
        private static readonly ObstacleKind[] Kinds =
            { ObstacleKind.Stairs, ObstacleKind.Barricade, ObstacleKind.Cage, ObstacleKind.PoliceCar };
        private static readonly Lane[] Lanes = { Lane.Left, Lane.Center, Lane.Right };

        private readonly SegmentGeneratorSettings s;

        public SeededSegmentLayoutGenerator(SegmentGeneratorSettings settings)
        {
            s = settings;
        }

        public SegmentLayout Next(int segmentIndex)
        {
            var rng = new Random(unchecked(s.Seed * 486187739 + segmentIndex * 1000003));
            var obstacles = new List<ObstaclePlacement>();
            var coins = new List<CoinPlacement>();

            var placeObstacles = segmentIndex >= s.WarmupSegments;
            for (var d = s.FirstObstacleOffset; d < s.SegmentLength; d += s.ObstacleSpacing)
            {
                var lane = Lanes[rng.Next(Lanes.Length)];
                if (placeObstacles)
                {
                    obstacles.Add(new ObstaclePlacement(Kinds[rng.Next(Kinds.Length)], lane, d));
                }

                // 코인 줄은 장애물과 다른 레인에 둔다 — 장애물을 피하는 쪽으로 자연스럽게 유도.
                var coinLane = Lanes[(Array.IndexOf(Lanes, lane) + 1 + rng.Next(Lanes.Length - 1)) % Lanes.Length];
                for (var c = 0; c < s.CoinsPerRow; c++)
                {
                    var cd = d + s.CoinRowOffset + c * s.CoinSpacing;
                    if (cd >= s.SegmentLength) break;
                    coins.Add(new CoinPlacement(coinLane, cd));
                }
            }

            return new SegmentLayout(s.SegmentLength, obstacles, coins);
        }
    }
}
