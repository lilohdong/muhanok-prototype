#nullable enable
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Application.Ports;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using Muhanok.Presentation.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Muhanok.Composition
{
    /// 합성 루트. 모든 계층을 알고, 아무도 이것을 모른다. 로직 없음 — 배선만.
    /// 씬 준비: CLAUDE.md §14.1. 입력 소스 전환: GameConfig.inputSource.
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfig? gameConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            var cfg = gameConfig != null ? gameConfig : ScriptableObject.CreateInstance<GameConfig>();
            var tuning = cfg.tuning != null ? cfg.tuning : ScriptableObject.CreateInstance<TuningProfile>();
            var presentation = cfg.presentation != null ? cfg.presentation : ScriptableObject.CreateInstance<PresentationProfile>();

            builder.RegisterInstance(cfg.ToRunTuning());
            builder.RegisterInstance(cfg.ToTrackSettings());
            builder.RegisterInstance(cfg.ToGeneratorSettings());
            builder.RegisterInstance(tuning);
            builder.RegisterInstance(presentation);

            builder.Register<IClock, UnityClock>(Lifetime.Singleton);
            builder.Register<LatencyTelemetry>(Lifetime.Singleton)
                .WithParameter<double>(cfg.latencySmoothing)
                .As<ITelemetrySink, ILatencyReadout>();
            builder.Register<ISegmentLayoutGenerator, SeededSegmentLayoutGenerator>(Lifetime.Singleton);
            builder.Register<Track>(Lifetime.Singleton);
            builder.Register<WorldPresenter>(Lifetime.Singleton).As<IRunPresenter, ITrackPresenter>();
            builder.Register<RunSession>(Lifetime.Singleton);

            RegisterInputSource(builder, cfg);

            builder.RegisterComponentOnNewGameObject<HudView>(Lifetime.Singleton, "HUD");
            builder.RegisterComponentOnNewGameObject<GameLoopBehaviour>(Lifetime.Singleton, "GameLoop");

            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<HudView>().Construct(
                    presentation,
                    container.Resolve<ILatencyReadout>(),
                    container.Resolve<IPoseStatus>(),
                    cfg.inputSource.ToString(),
                    cfg.restartAfterSeconds);
                container.Resolve<GameLoopBehaviour>().Construct(
                    container.Resolve<RunSession>(),
                    container.Resolve<IClock>(),
                    container.Resolve<IReadOnlyList<IPerFrame>>(),
                    container.Resolve<IPoseStatus>(),
                    container.Resolve<HudView>(),
                    cfg.restartAfterSeconds);
            });
        }

        /// 여기 한 곳만 바꾸면 키보드 ↔ 카메라가 바뀐다. 게임 코드는 둘을 구분하지 못한다.
        private static void RegisterInputSource(IContainerBuilder builder, GameConfig cfg)
        {
            switch (cfg.inputSource)
            {
                case InputSourceKind.Pose:
                    builder.Register<UdpPoseReceiver>(Lifetime.Singleton).WithParameter<int>(cfg.udpPort);
                    builder.Register<PoseActionSource>(Lifetime.Singleton)
                        .As<IPlayerActionSource, IPerFrame, IPoseStatus>();
                    break;
                default:
                    builder.Register<KeyboardActionSource>(Lifetime.Singleton)
                        .As<IPlayerActionSource, IPerFrame>();
                    builder.Register<IPoseStatus, NullPoseStatus>(Lifetime.Singleton);
                    break;
            }
        }
    }
}
