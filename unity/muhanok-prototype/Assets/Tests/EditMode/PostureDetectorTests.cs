#nullable enable
using System.Collections.Generic;
using Muhanok.Application.Pose;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    /// 합성 프레임으로 상반신 모드 판정기를 검증한다. 실제 녹화 회귀는 RecordingRegressionTests.
    public sealed class PostureDetectorTests
    {
        private const float Fps = 30f;

        // 서 있는 기준 자세 (정규화 좌표, y는 아래로 증가). 어깨 너비 W = 0.30
        private const float NoseX = 0.50f, NoseY = 0.25f;
        private const float ShoulderY = 0.40f, ShoulderLX = 0.35f, ShoulderRX = 0.65f;
        private const float W = ShoulderRX - ShoulderLX;

        public static DetectorSettings UpperBodyDefaults(bool cameraFacesUser = true) => new DetectorSettings(
            BodyMode.UpperBody,
            jumpThreshold: 0.45f, kneeThreshold: 0.55f, kneeOppositeFactor: 0.6f, armRaiseThreshold: 0.20f,
            duckThreshold: 0.80f, stepThreshold: 0.50f,
            releaseFactor: 0.7f, confirmFrames: 2, cooldownSeconds: 0.3,
            baselineSampleCount: 30, baselineMinSamples: 10,
            minVisibility: 0.5f, lostTimeoutSeconds: 1.0, cameraFacesUser: cameraFacesUser);

        /// 몸 전체를 (dx, dy)만큼 옮기고, 선택적으로 한 손을 든 프레임.
        private static PoseFrame Frame(long seq, float dx = 0f, float dy = 0f, float? leftWristLift = null, float? rightWristLift = null)
        {
            var b = new PoseFrameBuilder()
                .Set(LandmarkIndex.Nose, NoseX + dx, NoseY + dy, 0.95f)
                .Set(LandmarkIndex.LeftShoulder, ShoulderLX + dx, ShoulderY + dy, 0.95f)
                .Set(LandmarkIndex.RightShoulder, ShoulderRX + dx, ShoulderY + dy, 0.95f);
            // 손목은 기본적으로 프레임 아래(안 보임)
            if (leftWristLift.HasValue) b.Set(LandmarkIndex.LeftWrist, ShoulderLX + dx, NoseY + dy - leftWristLift.Value, 0.9f);
            else b.Set(LandmarkIndex.LeftWrist, ShoulderLX + dx, 1.1f, 0.1f);
            if (rightWristLift.HasValue) b.Set(LandmarkIndex.RightWrist, ShoulderRX + dx, NoseY + dy - rightWristLift.Value, 0.9f);
            else b.Set(LandmarkIndex.RightWrist, ShoulderRX + dx, 1.1f, 0.1f);
            return b.Build(seq, seq / Fps);
        }

        private sealed class Harness
        {
            public readonly PostureDetector Detector;
            public readonly List<PlayerAction> Actions = new List<PlayerAction>();
            private readonly PlayerAction[] scratch = new PlayerAction[8];
            private long seq;

            public Harness(DetectorSettings s) { Detector = new PostureDetector(s); }

            public void Feed(PoseFrame f)
            {
                var n = Detector.Step(f, scratch);
                for (var i = 0; i < n; i++) Actions.Add(scratch[i]);
            }

            public void Idle(int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++)); }
            public void Jump(float heightInW, int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++, dy: -heightInW * W)); }
            public void Duck(float dropInW, int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++, dy: dropInW * W)); }
            public void Shift(float dxInW, int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++, dx: dxInW * W)); }
            public void Move(float dxInW, float dyInW, int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++, dx: dxInW * W, dy: dyInW * W)); }
            public void RaiseLeft(float liftInW, int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++, leftWristLift: liftInW * W)); }
            public void RaiseBoth(float liftInW, int frames) { for (var i = 0; i < frames; i++) Feed(Frame(seq++, leftWristLift: liftInW * W, rightWristLift: liftInW * W)); }
            public void Lost(int frames) { for (var i = 0; i < frames; i++) Feed(PoseFrame.Lost(seq, seq++ / Fps)); }

            public int Count(PlayerAction a) => Actions.FindAll(x => x == a).Count;
        }

        [Test]
        public void NoDetection_BeforeBaseline()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(5);
            Assert.That(h.Detector.HasBaseline, Is.False);
            h.Jump(1.0f, 5);
            Assert.That(h.Actions, Is.Empty);
        }

        [Test]
        public void TenJumps_DetectedExactlyTen()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            for (var i = 0; i < 10; i++)
            {
                h.Jump(0.8f, 8);
                h.Idle(30);
            }
            Assert.That(h.Count(PlayerAction.Jump), Is.EqualTo(10));
            Assert.That(h.Actions.Count, Is.EqualTo(10), "no false positives: " + string.Join(",", h.Actions));
        }

        [Test]
        public void Jump_SingleFrameSpike_Ignored()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.Jump(1.0f, 1);
            h.Idle(30);
            Assert.That(h.Actions, Is.Empty);
        }

        [Test]
        public void Jump_ReportsAirborneWhileUp_ThenGrounded()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.Jump(0.8f, 4);
            Assert.That(h.Detector.Posture, Is.EqualTo(PostureState.Airborne));
            h.Idle(4);
            Assert.That(h.Detector.Posture, Is.EqualTo(PostureState.Grounded));
        }

        [Test]
        public void TenDucks_DetectedExactlyTen()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            for (var i = 0; i < 10; i++)
            {
                h.Duck(1.2f, 12);
                h.Idle(30);
            }
            Assert.That(h.Count(PlayerAction.Duck), Is.EqualTo(10));
            Assert.That(h.Actions.Count, Is.EqualTo(10));
        }

        [Test]
        public void CrouchBeforeJump_DoesNotTriggerDuck()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.Duck(0.3f, 4);   // 점프 준비 움츠림
            h.Jump(0.8f, 8);
            h.Idle(30);
            Assert.That(h.Count(PlayerAction.Duck), Is.EqualTo(0));
            Assert.That(h.Count(PlayerAction.Jump), Is.EqualTo(1));
        }

        [Test]
        public void TenArmRaises_DetectedAsKneeRaise()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            for (var i = 0; i < 10; i++)
            {
                h.RaiseLeft(0.5f, 10);
                h.Idle(30);
            }
            Assert.That(h.Count(PlayerAction.KneeRaise), Is.EqualTo(10));
            Assert.That(h.Actions.Count, Is.EqualTo(10));
        }

        [Test]
        public void BothArmsRaised_IsNotKneeRaise()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.RaiseBoth(0.5f, 10);
            h.Idle(30);
            Assert.That(h.Count(PlayerAction.KneeRaise), Is.EqualTo(0));
        }

        [Test]
        public void StepLeftAndBack_EmitsLeftThenRight()
        {
            var h = new Harness(UpperBodyDefaults(cameraFacesUser: true));
            h.Idle(30);
            h.Shift(+0.8f, 10);   // 사용자의 왼쪽 = 이미지 x 증가
            Assert.That(h.Actions, Is.EqualTo(new[] { PlayerAction.StepLeft }));
            Assert.That(h.Detector.Zone, Is.EqualTo(Lane.Left));
            h.Shift(0f, 10);
            Assert.That(h.Actions, Is.EqualTo(new[] { PlayerAction.StepLeft, PlayerAction.StepRight }));
            Assert.That(h.Detector.Zone, Is.EqualTo(Lane.Center));
        }

        [Test]
        public void MirroredCamera_FlipsStepDirection()
        {
            var h = new Harness(UpperBodyDefaults(cameraFacesUser: false));
            h.Idle(30);
            h.Shift(+0.8f, 10);
            Assert.That(h.Actions, Is.EqualTo(new[] { PlayerAction.StepRight }));
        }

        [Test]
        public void StayingInSideLane_DoesNotDriftBaseline()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.Shift(+0.8f, 120);  // 4초 동안 왼쪽 레인에 서 있음
            h.Shift(0f, 10);
            Assert.That(h.Actions, Is.EqualTo(new[] { PlayerAction.StepLeft, PlayerAction.StepRight }));
        }

        [Test]
        public void ZoneJitterAtBoundary_DoesNotSpam()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            // 확실히 들어간 뒤 진입 임계 0.5W 근처에서 떨림: 0.55 ↔ 0.45. 히스테리시스(해제 0.35W) 덕에 되돌아오지 않아야 한다.
            h.Shift(0.55f, 3);
            for (var i = 0; i < 20; i++) h.Shift(i % 2 == 0 ? 0.55f : 0.45f, 1);
            Assert.That(h.Count(PlayerAction.StepLeft), Is.EqualTo(1));
            Assert.That(h.Count(PlayerAction.StepRight), Is.EqualTo(0));
        }

        [Test]
        public void Lost_ResetsPosture_KeepsZone()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.Shift(+0.8f, 10);
            h.Move(0.8f, -0.8f, 3);
            Assert.That(h.Detector.Posture, Is.EqualTo(PostureState.Airborne));
            h.Lost(60);
            Assert.That(h.Detector.IsTracking, Is.False);
            Assert.That(h.Detector.Posture, Is.EqualTo(PostureState.Grounded));
            Assert.That(h.Detector.Zone, Is.EqualTo(Lane.Left));
        }

        [Test]
        public void Cooldown_BlocksImmediateRepeat()
        {
            var h = new Harness(UpperBodyDefaults());
            h.Idle(30);
            h.Jump(0.8f, 3);
            h.Idle(2);          // 해제
            h.Jump(0.8f, 3);    // 0.3초 안 — 무시돼야 한다
            Assert.That(h.Count(PlayerAction.Jump), Is.EqualTo(1));
        }
    }
}
