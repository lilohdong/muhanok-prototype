#nullable enable
using System;
using Muhanok.Application.Pose;
using Muhanok.Application.Ports;
using Muhanok.Domain;

namespace Muhanok.Infrastructure
{
    /// 카메라 입력 소스: UDP 최신 패킷 → PostureDetector → PlayerAction 이벤트. 메인 스레드에서 OnFrame으로 돈다.
    /// 게임 로직은 이것이 KeyboardActionSource와 다른지 모른다.
    public sealed class PoseActionSource : IPlayerActionSource, IPerFrame, IPoseStatus
    {
        private const int MaxActionsPerFrame = 8;

        private readonly UdpPoseReceiver receiver;
        private readonly ITelemetrySink telemetry;
        private readonly IClock clock;
        private readonly TuningProfile profile;
        private readonly PlayerAction[] scratch = new PlayerAction[MaxActionsPerFrame];

        private PostureDetector detector;
        private int settingsHash;
        private double lastPacketAt = double.NegativeInfinity;

        public event Action<PlayerAction>? ActionDetected;

        public PoseActionSource(UdpPoseReceiver receiver, ITelemetrySink telemetry, IClock clock, TuningProfile profile)
        {
            this.receiver = receiver;
            this.telemetry = telemetry;
            this.clock = clock;
            this.profile = profile;
            detector = new PostureDetector(profile.ToSettings());
            settingsHash = SettingsHash(profile);
        }

        // ── IPoseStatus ──
        public bool IsEnabled => true;
        public bool IsReceiving => clock.NowSeconds - lastPacketAt < profile.lostTimeoutSeconds;
        public bool IsTracking => IsReceiving && detector.IsTracking;
        public PostureState DetectedPosture => detector.Posture;
        public Lane DetectedZone => detector.Zone;
        public PoseFrame? LastFrame { get; private set; }
        public float JumpRatio => detector.LastJumpRatio;
        public float LiftRatio => detector.LastLiftRatio;
        public float DuckRatio => detector.LastDuckRatio;
        public float StepRatio => detector.LastStepRatio;

        public void OnFrame(float deltaSeconds)
        {
            ReloadIfTuned();

            var received = receiver.TakeLatest();
            if (received == null) return;

            lastPacketAt = clock.NowSeconds;
            LastFrame = received.Frame;
            // 같은 PC에서만 의미 있는 상대 지표. 클럭 차이로 절대값은 부정확할 수 있다.
            telemetry.RecordLatency((received.ReceivedAtUnixSeconds - received.Frame.Time) * 1000.0);

            var n = detector.Step(received.Frame, scratch);
            for (var i = 0; i < n; i++) ActionDetected?.Invoke(scratch[i]);
        }

        /// 인스펙터에서 TuningProfile을 만지면 판정기를 새 값으로 다시 만든다. 베이스라인은 다시 쌓인다(≈1초).
        private void ReloadIfTuned()
        {
            var h = SettingsHash(profile);
            if (h == settingsHash) return;
            settingsHash = h;
            detector = new PostureDetector(profile.ToSettings());
        }

        private static int SettingsHash(TuningProfile p)
        {
            var h = new HashCode();
            h.Add(p.bodyMode);
            h.Add(p.cameraFacesUser);
            h.Add(p.jumpThreshold);
            h.Add(p.kneeThreshold);
            h.Add(p.kneeOppositeFactor);
            h.Add(p.armRaiseThreshold);
            h.Add(p.duckThreshold);
            h.Add(p.stepThreshold);
            h.Add(p.stepHeadWeight);
            h.Add(p.releaseFactor);
            h.Add(p.confirmFrames);
            h.Add(p.cooldownSeconds);
            h.Add(p.baselineSampleCount);
            h.Add(p.baselineMinSamples);
            h.Add(p.minVisibility);
            h.Add(p.lostTimeoutSeconds);
            return h.ToHashCode();
        }
    }

    /// 키보드 모드에서 HUD가 읽는 빈 상태.
    public sealed class NullPoseStatus : IPoseStatus
    {
        public bool IsEnabled => false;
        public bool IsReceiving => false;
        public bool IsTracking => false;
        public PostureState DetectedPosture => PostureState.Grounded;
        public Lane DetectedZone => Lane.Center;
        public PoseFrame? LastFrame => null;
        public float JumpRatio => 0f;
        public float LiftRatio => 0f;
        public float DuckRatio => 0f;
        public float StepRatio => 0f;
    }
}
