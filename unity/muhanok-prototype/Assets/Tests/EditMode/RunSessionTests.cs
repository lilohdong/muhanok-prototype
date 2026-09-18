#nullable enable
using System;
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Application.Ports;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    /// 유스케이스 배선 확인: 장애물을 지나는 순간 한 번만 판정, 코인은 같은 레인에서만, 잡히면 종료.
    public sealed class RunSessionTests
    {
        private sealed class FakeActions : IPlayerActionSource
        {
            public event Action<PlayerAction>? ActionDetected;
            public void Fire(PlayerAction a) => ActionDetected?.Invoke(a);
        }

        private sealed class FakeGenerator : ISegmentLayoutGenerator
        {
            public SegmentLayout Layout = SegmentLayout.Empty(40f);
            public SegmentLayout Next(int segmentIndex) => segmentIndex == 0 ? Layout : SegmentLayout.Empty(40f);
        }

        private sealed class Spy : IRunPresenter, ITrackPresenter
        {
            public int Stumbles, CoinsCollected, Ended, Spawned, Retired, CoinsTaken;
            public RunState? Last;
            public void OnStateChanged(RunState state) => Last = state;
            public void OnPlayerStumbled() => Stumbles++;
            public void OnCoinCollected(int total) => CoinsCollected = total;
            public void OnRunEnded(int finalScore) => Ended++;
            public void OnSegmentSpawned(TrackSegment segment) => Spawned++;
            public void OnSegmentRetired(TrackSegment segment) => Retired++;
            public void OnCoinTaken(TrackSegment segment, int coinIndex) => CoinsTaken++;
        }

        private static readonly RunTuning Tuning = new RunTuning(
            speedStart: 10f, speedMax: 10f, speedGainPerMeter: 0f,
            chaserGapStart: 5f, chaserGapMax: 5f, stumblePenalty: 3f, gapRecoveryPerSecond: 0f,
            coinScore: 10, postureDuration: 0.5f);

        private static (RunSession, FakeActions, Spy) Make(SegmentLayout layout)
        {
            var actions = new FakeActions();
            var spy = new Spy();
            var gen = new FakeGenerator { Layout = layout };
            var track = new Track(gen, spy, new TrackSettings(100f, 10f));
            var session = new RunSession(actions, spy, track, Tuning);
            session.Start();
            return (session, actions, spy);
        }

        private static void Run(RunSession s, float seconds, float dt = 0.1f)
        {
            for (var t = 0f; t < seconds; t += dt) s.Tick(dt);
        }

        [Test]
        public void Barricade_WithoutJump_Stumbles_Once()
        {
            var layout = new SegmentLayout(40f, new[] { new ObstaclePlacement(ObstacleKind.Barricade, Lane.Center, 10f) }, Array.Empty<CoinPlacement>());
            var (session, _, spy) = Make(layout);
            Run(session, 2f);
            Assert.That(spy.Stumbles, Is.EqualTo(1));
            Assert.That(session.State.ChaserGap, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(session.State.IsOver, Is.False);
        }

        [Test]
        public void Barricade_WithTimedJump_Clears()
        {
            var layout = new SegmentLayout(40f, new[] { new ObstaclePlacement(ObstacleKind.Barricade, Lane.Center, 10f) }, Array.Empty<CoinPlacement>());
            var (session, actions, spy) = Make(layout);
            Run(session, 0.8f);               // 8m
            actions.Fire(PlayerAction.Jump);  // 0.5초 동안 Airborne → 13m까지
            Run(session, 1.2f);
            Assert.That(spy.Stumbles, Is.EqualTo(0));
        }

        [Test]
        public void Jump_ExpiresAfterPostureDuration()
        {
            var (session, actions, _) = Make(SegmentLayout.Empty(40f));
            actions.Fire(PlayerAction.Jump);
            Assert.That(session.State.Posture, Is.EqualTo(PostureState.Airborne));
            Run(session, 0.4f);
            Assert.That(session.State.Posture, Is.EqualTo(PostureState.Airborne));
            Run(session, 0.2f);
            Assert.That(session.State.Posture, Is.EqualTo(PostureState.Grounded));
        }

        [Test]
        public void PoliceCar_SideStepClears()
        {
            var layout = new SegmentLayout(40f, new[] { new ObstaclePlacement(ObstacleKind.PoliceCar, Lane.Center, 10f) }, Array.Empty<CoinPlacement>());
            var (session, actions, spy) = Make(layout);
            actions.Fire(PlayerAction.StepLeft);
            Run(session, 2f);
            Assert.That(spy.Stumbles, Is.EqualTo(0));
            Assert.That(session.State.CurrentLane, Is.EqualTo(Lane.Left));
        }

        [Test]
        public void LaneShift_ClampsAtEdges()
        {
            var (session, actions, _) = Make(SegmentLayout.Empty(40f));
            actions.Fire(PlayerAction.StepRight);
            actions.Fire(PlayerAction.StepRight);
            actions.Fire(PlayerAction.StepRight);
            Assert.That(session.State.CurrentLane, Is.EqualTo(Lane.Right));
        }

        [Test]
        public void Coins_OnlyInPlayerLane()
        {
            var layout = new SegmentLayout(40f, Array.Empty<ObstaclePlacement>(), new[]
            {
                new CoinPlacement(Lane.Center, 5f),
                new CoinPlacement(Lane.Left, 6f),
                new CoinPlacement(Lane.Center, 7f),
            });
            var (session, _, spy) = Make(layout);
            Run(session, 2f);
            Assert.That(session.State.Coins, Is.EqualTo(2));
            Assert.That(spy.CoinsTaken, Is.EqualTo(2));
            Assert.That(session.State.Score, Is.EqualTo((int)Math.Floor(session.State.DistanceTravelled) + 20));
        }

        [Test]
        public void TwoStumbles_EndRun()
        {
            var layout = new SegmentLayout(40f, new[]
            {
                new ObstaclePlacement(ObstacleKind.Cage, Lane.Center, 5f),
                new ObstaclePlacement(ObstacleKind.Cage, Lane.Center, 15f),
            }, Array.Empty<CoinPlacement>());
            var (session, actions, spy) = Make(layout);
            Run(session, 3f);
            Assert.That(session.State.IsOver, Is.True);
            Assert.That(spy.Ended, Is.EqualTo(1));
            var distanceAtEnd = session.State.DistanceTravelled;
            actions.Fire(PlayerAction.Jump);
            Run(session, 1f);
            Assert.That(session.State.DistanceTravelled, Is.EqualTo(distanceAtEnd), "no progress after game over");
        }

        [Test]
        public void Segments_SpawnAheadAndRetireBehind()
        {
            var (session, _, spy) = Make(SegmentLayout.Empty(40f));
            Assert.That(spy.Spawned, Is.EqualTo(3));   // 0..100m 앞을 덮으려면 40m 구간 3개
            Run(session, 6f);                           // 60m 전진 → 첫 구간(0~40)은 50m 뒤 → 회수
            Assert.That(spy.Retired, Is.EqualTo(1));
        }
    }
}
