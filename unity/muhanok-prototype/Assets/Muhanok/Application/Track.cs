#nullable enable
using System.Collections.Generic;
using Muhanok.Application.Ports;
using Muhanok.Domain;

namespace Muhanok.Application
{
    /// 트랙에 실제로 놓인 구간 하나. 배치도(불변) + 판정 진행 상태(가변, 이 클래스 안에서만).
    public sealed class TrackSegment
    {
        public int Index { get; }
        public float StartDistance { get; }
        public SegmentLayout Layout { get; }
        public float EndDistance => StartDistance + Layout.Length;

        private readonly bool[] obstacleResolved;
        private readonly bool[] coinTaken;

        public TrackSegment(int index, float startDistance, SegmentLayout layout)
        {
            Index = index;
            StartDistance = startDistance;
            Layout = layout;
            obstacleResolved = new bool[layout.Obstacles.Count];
            coinTaken = new bool[layout.Coins.Count];
        }

        public bool IsObstacleResolved(int i) => obstacleResolved[i];
        public bool IsCoinTaken(int i) => coinTaken[i];
        internal void ResolveObstacle(int i) => obstacleResolved[i] = true;
        internal void TakeCoin(int i) => coinTaken[i] = true;

        public float ObstacleDistance(int i) => StartDistance + Layout.Obstacles[i].DistanceFromSegmentStart;
        public float CoinDistance(int i) => StartDistance + Layout.Coins[i].DistanceFromSegmentStart;
    }

    public sealed class TrackSettings
    {
        /// 플레이어 앞으로 이만큼은 항상 구간이 깔려 있어야 한다.
        public float SpawnAheadDistance { get; }
        /// 플레이어 뒤로 이만큼 지나간 구간은 회수한다.
        public float RetireBehindDistance { get; }

        public TrackSettings(float spawnAheadDistance, float retireBehindDistance)
        {
            SpawnAheadDistance = spawnAheadDistance;
            RetireBehindDistance = retireBehindDistance;
        }
    }

    /// 무한 트랙. 앞으로 구간을 깔고 뒤의 구간을 회수한다. 배치도 자체는 생성기 포트가 만든다.
    public sealed class Track
    {
        private readonly ISegmentLayoutGenerator generator;
        private readonly ITrackPresenter presenter;
        private readonly TrackSettings settings;
        private readonly List<TrackSegment> active = new List<TrackSegment>();

        private int nextIndex;
        private float nextStart;

        public IReadOnlyList<TrackSegment> Active => active;

        public Track(ISegmentLayoutGenerator generator, ITrackPresenter presenter, TrackSettings settings)
        {
            this.generator = generator;
            this.presenter = presenter;
            this.settings = settings;
        }

        public void TakeCoin(TrackSegment segment, int coinIndex)
        {
            segment.TakeCoin(coinIndex);
            presenter.OnCoinTaken(segment, coinIndex);
        }

        public void Update(float playerDistance)
        {
            while (nextStart < playerDistance + settings.SpawnAheadDistance)
            {
                var layout = generator.Next(nextIndex);
                var segment = new TrackSegment(nextIndex, nextStart, layout);
                active.Add(segment);
                presenter.OnSegmentSpawned(segment);
                nextIndex++;
                nextStart += layout.Length;
            }

            // 가장 오래된 구간부터 순서대로 회수한다. 리스트 앞쪽이 항상 가장 뒤에 있는 구간이다.
            while (active.Count > 0 && active[0].EndDistance < playerDistance - settings.RetireBehindDistance)
            {
                var retired = active[0];
                active.RemoveAt(0);
                presenter.OnSegmentRetired(retired);
            }
        }
    }
}
