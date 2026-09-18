#nullable enable
using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    public readonly struct ObstaclePlacement
    {
        public readonly ObstacleKind Kind;
        public readonly Lane Lane;
        public readonly float DistanceFromSegmentStart;

        public ObstaclePlacement(ObstacleKind kind, Lane lane, float distanceFromSegmentStart)
        {
            Kind = kind;
            Lane = lane;
            DistanceFromSegmentStart = distanceFromSegmentStart;
        }
    }

    public readonly struct CoinPlacement
    {
        public readonly Lane Lane;
        public readonly float DistanceFromSegmentStart;

        public CoinPlacement(Lane lane, float distanceFromSegmentStart)
        {
            Lane = lane;
            DistanceFromSegmentStart = distanceFromSegmentStart;
        }
    }

    /// 한 구간의 배치도. 프리팹이 아니라 '데이터'다.
    public sealed class SegmentLayout
    {
        public float Length { get; }
        public IReadOnlyList<ObstaclePlacement> Obstacles { get; }
        public IReadOnlyList<CoinPlacement> Coins { get; }

        public SegmentLayout(float length, IReadOnlyList<ObstaclePlacement> obstacles, IReadOnlyList<CoinPlacement> coins)
        {
            Length = length;
            Obstacles = obstacles;
            Coins = coins;
        }

        public static SegmentLayout Empty(float length) =>
            new SegmentLayout(length, Array.Empty<ObstaclePlacement>(), Array.Empty<CoinPlacement>());
    }
}
