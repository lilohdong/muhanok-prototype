#nullable enable
using System;
using Muhanok.Application.Ports;
using Muhanok.Domain;

namespace Muhanok.Application
{
    /// 한 판을 굴리는 유스케이스. 입력(동작 이벤트)과 시간(Tick)을 받아 Domain 규칙을 호출하고 결과를 프레젠터로 내보낸다.
    public sealed class RunSession : IDisposable
    {
        private readonly IPlayerActionSource actions;
        private readonly IRunPresenter presenter;
        private readonly Track track;
        private readonly RunTuning tuning;

        private RunState state;
        private float postureRemaining;

        public RunState State => state;

        public RunSession(
            IPlayerActionSource actions,
            IRunPresenter presenter,
            Track track,
            RunTuning tuning)
        {
            this.actions = actions;
            this.presenter = presenter;
            this.track = track;
            this.tuning = tuning;

            state = RunState.Initial(tuning);
            actions.ActionDetected += HandleAction;
        }

        public void Start()
        {
            track.Update(state.DistanceTravelled);
            presenter.OnStateChanged(state);
        }

        public void HandleAction(PlayerAction action)
        {
            if (state.IsOver) return;

            switch (action)
            {
                case PlayerAction.Jump:
                    EnterPosture(PostureState.Airborne);
                    break;
                case PlayerAction.KneeRaise:
                    EnterPosture(PostureState.KneeRaised);
                    break;
                case PlayerAction.Duck:
                    EnterPosture(PostureState.Ducking);
                    break;
                case PlayerAction.StepLeft:
                    state = RunRules.ShiftLane(state, -1);
                    break;
                case PlayerAction.StepRight:
                    state = RunRules.ShiftLane(state, +1);
                    break;
            }

            presenter.OnStateChanged(state);
        }

        private void EnterPosture(PostureState posture)
        {
            state = RunRules.SetPosture(state, posture);
            postureRemaining = tuning.PostureDuration;
        }

        public void Tick(float deltaSeconds)
        {
            if (state.IsOver || deltaSeconds <= 0f) return;

            var before = state.DistanceTravelled;
            state = RunRules.Advance(state, deltaSeconds, tuning);
            ExpirePosture(deltaSeconds);
            track.Update(state.DistanceTravelled);
            ResolveCrossings(before, state.DistanceTravelled);

            presenter.OnStateChanged(state);
            if (state.IsOver) presenter.OnRunEnded(state.Score);
        }

        private void ExpirePosture(float deltaSeconds)
        {
            if (state.Posture == PostureState.Grounded) return;
            postureRemaining -= deltaSeconds;
            if (postureRemaining <= 0f) state = RunRules.SetPosture(state, PostureState.Grounded);
        }

        /// 이번 틱에 플레이어가 지나친 장애물/코인을 한 번씩 판정한다. 판정 시점은 "지나치는 순간" 딱 한 번이다.
        private void ResolveCrossings(float from, float to)
        {
            var segments = track.Active;
            for (var s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];
                if (segment.StartDistance > to || segment.EndDistance < from) continue;

                ResolveObstacles(segment, from, to);
                ResolveCoins(segment, from, to);
                if (state.IsOver) return;
            }
        }

        private void ResolveObstacles(TrackSegment segment, float from, float to)
        {
            var obstacles = segment.Layout.Obstacles;
            for (var i = 0; i < obstacles.Count; i++)
            {
                if (segment.IsObstacleResolved(i)) continue;
                var d = segment.ObstacleDistance(i);
                if (d <= from || d > to) continue;

                segment.ResolveObstacle(i);
                var o = obstacles[i];
                if (ClearanceRule.Clears(o.Kind, o.Lane, state.CurrentLane, state.Posture)) continue;

                state = RunRules.Stumble(state, tuning);
                presenter.OnPlayerStumbled();
                if (state.IsOver) return;
            }
        }

        private void ResolveCoins(TrackSegment segment, float from, float to)
        {
            var coins = segment.Layout.Coins;
            for (var i = 0; i < coins.Count; i++)
            {
                if (segment.IsCoinTaken(i)) continue;
                var d = segment.CoinDistance(i);
                if (d <= from || d > to) continue;
                if (coins[i].Lane != state.CurrentLane) continue;

                track.TakeCoin(segment, i);
                state = RunRules.CollectCoin(state, tuning);
                presenter.OnCoinCollected(state.Coins);
            }
        }

        public void Dispose()
        {
            actions.ActionDetected -= HandleAction;
        }
    }
}
