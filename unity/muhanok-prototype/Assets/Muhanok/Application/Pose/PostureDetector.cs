#nullable enable
using System;
using Muhanok.Domain;

namespace Muhanok.Application.Pose
{
    /// 프레임을 먹고 자세(PostureState)와 동작 이벤트(PlayerAction)를 내는 순수 상태 기계. Unity를 모른다.
    ///
    /// 좌표 규약: MediaPipe의 y는 아래로 갈수록 커진다. 이 파일 전체에서 "y가 작아지면 위로 올라간 것"이다.
    /// 임계값 규약: 전부 기준 단위(UpperBody = 어깨 너비 W, FullBody = torso)의 배수. 절대 좌표 비교는 없다.
    public sealed class PostureDetector
    {
        private readonly DetectorSettings s;

        private readonly MedianBaseline unitBase;
        private readonly MedianBaseline bodyYBase;
        private readonly MedianBaseline bodyXBase;
        private readonly MedianBaseline noseYBase;
        private readonly MedianBaseline stepUnitBase;

        private readonly GestureChannel jump;
        private readonly GestureChannel lift;
        private readonly GestureChannel duck;

        private Lane zone = Lane.Center;
        private Lane zoneCandidate = Lane.Center;
        private int zoneConsecutive;

        private double lostSince = double.NaN;
        private bool tracking;

        public PostureState Posture { get; private set; } = PostureState.Grounded;
        public Lane Zone => zone;
        public bool IsTracking => tracking;
        public bool HasBaseline => unitBase.Count >= s.BaselineMinSamples;

        /// 마지막 프레임의 정규화된 신호(디버그 HUD용).
        public float LastJumpRatio { get; private set; }
        public float LastLiftRatio { get; private set; }
        public float LastDuckRatio { get; private set; }
        public float LastStepRatio { get; private set; }

        public PostureDetector(DetectorSettings settings)
        {
            s = settings;
            unitBase = new MedianBaseline(s.BaselineSampleCount);
            bodyYBase = new MedianBaseline(s.BaselineSampleCount);
            bodyXBase = new MedianBaseline(s.BaselineSampleCount);
            noseYBase = new MedianBaseline(s.BaselineSampleCount);
            stepUnitBase = new MedianBaseline(s.BaselineSampleCount);
            jump = new GestureChannel(s.ConfirmFrames, s.ReleaseFactor, s.CooldownSeconds);
            lift = new GestureChannel(s.ConfirmFrames, s.ReleaseFactor, s.CooldownSeconds);
            duck = new GestureChannel(s.ConfirmFrames, s.ReleaseFactor, s.CooldownSeconds);
        }

        /// 프레임 하나를 처리하고 이번 프레임에 인정된 동작을 output에 쓴다. 반환값은 쓴 개수. 할당 없음.
        public int Step(PoseFrame frame, PlayerAction[] output)
        {
            var count = 0;
            if (!TryExtract(frame, out var sig))
            {
                HandleLost(frame.Time);
                return 0;
            }

            lostSince = double.NaN;

            var idle = !jump.Active && !lift.Active && !duck.Active;
            if (idle) UpdateBaseline(sig);

            if (!HasBaseline)
            {
                tracking = false;
                return 0;
            }
            tracking = true;

            var unit = unitBase.Median;
            if (unit <= 0f) return 0;

            count += UpdateVertical(sig, unit, frame.Time, output, count);
            count += UpdateZone(sig, stepUnitBase.Median, output, count);
            Posture = ResolvePosture();
            return count;
        }

        private void HandleLost(double time)
        {
            if (double.IsNaN(lostSince)) lostSince = time;
            if (time - lostSince < s.LostTimeoutSeconds) return;

            // 오래 끊기면 자세는 서 있는 것으로 되돌린다. 존(레인)은 유지한다 — 돌아왔을 때 실제 몸 위치와 게임 레인이 다시 맞물리도록.
            jump.Reset();
            lift.Reset();
            duck.Reset();
            Posture = PostureState.Grounded;
            tracking = false;
        }

        private void UpdateBaseline(in Signals sig)
        {
            unitBase.Add(sig.Unit);
            bodyYBase.Add(sig.BodyY);
            noseYBase.Add(sig.NoseY);
            stepUnitBase.Add(sig.StepUnit);
            // x 기준은 가운데 존에 있을 때만. 옆 레인에 서 있는 동안 기준이 따라가면 돌아올 때 이벤트가 안 난다.
            if (zone == Lane.Center) bodyXBase.Add(sig.BodyX);
        }

        private int UpdateVertical(in Signals sig, float unit, double time, PlayerAction[] output, int offset)
        {
            var n = 0;

            // 위로 뜨면 y가 작아진다.
            LastJumpRatio = (bodyYBase.Median - sig.BodyY) / (unit * s.JumpThreshold);
            if (jump.Update(LastJumpRatio, time)) output[offset + n++] = PlayerAction.Jump;

            LastDuckRatio = (sig.NoseY - noseYBase.Median) / (unit * s.DuckThreshold);
            if (duck.Update(LastDuckRatio, time)) output[offset + n++] = PlayerAction.Duck;

            LastLiftRatio = LiftRatio(sig, unit);
            if (lift.Update(LastLiftRatio, time)) output[offset + n++] = PlayerAction.KneeRaise;

            return n;
        }

        /// 한쪽만 들렸는가. 활성 중에는 "반대쪽" 조건을 보지 않는다 — 해제 판정이 떨리지 않게.
        private float LiftRatio(in Signals sig, float unit)
        {
            var threshold = s.Mode == BodyMode.FullBody ? s.KneeThreshold : s.ArmRaiseThreshold;
            var left = sig.LiftLeftValid ? sig.LiftLeft / (unit * threshold) : float.NegativeInfinity;
            var right = sig.LiftRightValid ? sig.LiftRight / (unit * threshold) : float.NegativeInfinity;

            if (lift.Active) return Math.Max(left, right);

            var oppositeLimit = s.Mode == BodyMode.FullBody
                ? s.KneeOppositeFactor                       // 반대쪽 무릎 비율 < T_knee × 0.6
                : sig.OppositeLimitUpper / (unit * threshold); // 반대쪽 손목은 어깨선 아래

            var best = float.NegativeInfinity;
            if (sig.LiftLeftValid && right < oppositeLimit) best = Math.Max(best, left);
            if (sig.LiftRightValid && left < oppositeLimit) best = Math.Max(best, right);
            return float.IsNegativeInfinity(best) ? 0f : best;
        }

        private int UpdateZone(in Signals sig, float unit, PlayerAction[] output, int offset)
        {
            if (unit <= 0f) return 0;
            var dx = sig.BodyX - bodyXBase.Median;
            var towardUserLeft = s.CameraFacesUser ? dx : -dx;
            LastStepRatio = towardUserLeft / (unit * s.StepThreshold);

            var target = TargetZone(LastStepRatio);
            if (target == zone)
            {
                zoneCandidate = zone;
                zoneConsecutive = 0;
                return 0;
            }
            if (target != zoneCandidate)
            {
                zoneCandidate = target;
                zoneConsecutive = 0;
            }
            zoneConsecutive++;
            if (zoneConsecutive < s.ConfirmFrames) return 0;

            var n = 0;
            var step = (int)target - (int)zone;
            var action = step < 0 ? PlayerAction.StepLeft : PlayerAction.StepRight;
            for (var i = 0; i < Math.Abs(step); i++) output[offset + n++] = action;
            zone = target;
            zoneConsecutive = 0;
            return n;
        }

        private Lane TargetZone(float leftRatio)
        {
            switch (zone)
            {
                case Lane.Left:
                    if (leftRatio <= -1f) return Lane.Right;
                    return leftRatio < s.ReleaseFactor ? Lane.Center : Lane.Left;
                case Lane.Right:
                    if (leftRatio >= 1f) return Lane.Left;
                    return leftRatio > -s.ReleaseFactor ? Lane.Center : Lane.Right;
                default:
                    if (leftRatio >= 1f) return Lane.Left;
                    if (leftRatio <= -1f) return Lane.Right;
                    return Lane.Center;
            }
        }

        private PostureState ResolvePosture()
        {
            if (jump.Active) return PostureState.Airborne;
            if (duck.Active) return PostureState.Ducking;
            if (lift.Active) return PostureState.KneeRaised;
            return PostureState.Grounded;
        }

        // ── 신호 추출 ──────────────────────────────────────────────────────────

        private struct Signals
        {
            public float Unit;   // 이번 프레임의 원시 기준 단위 (베이스라인 중앙값이 실제 단위가 된다)
            public float StepUnit; // 사이드 스텝은 두 모드 모두 어깨 너비 배수 (§8.3, §8.6)
            public float BodyY;  // 점프 기준 y (UpperBody: 어깨 중점, FullBody: 골반 중점)
            public float BodyX;  // 사이드 스텝 기준 x
            public float NoseY;
            public float LiftLeft, LiftRight;
            public bool LiftLeftValid, LiftRightValid;
            public float OppositeLimitUpper; // UpperBody: 어깨선까지의 들림량. 반대쪽 손목이 이보다 덜 들려야 한다
        }

        private bool TryExtract(PoseFrame f, out Signals sig)
        {
            sig = default;
            if (!f.Ok) return false;
            if (!Visible(f, LandmarkIndex.LeftShoulder, out var ls) || !Visible(f, LandmarkIndex.RightShoulder, out var rs)) return false;
            if (!Visible(f, LandmarkIndex.Nose, out var nose)) return false;

            var shoulderW = Math.Abs(ls.X - rs.X);
            var shoulderMidY = (ls.Y + rs.Y) * 0.5f;
            var shoulderMidX = (ls.X + rs.X) * 0.5f;
            sig.NoseY = nose.Y;
            sig.StepUnit = shoulderW;

            if (s.Mode == BodyMode.UpperBody)
            {
                sig.Unit = shoulderW;
                sig.BodyY = shoulderMidY;
                sig.BodyX = shoulderMidX;
                // 손목이 안 보이면(프레임 아래) "안 든 것"으로 본다. 든 손은 반드시 보인다.
                sig.LiftLeftValid = Visible(f, LandmarkIndex.LeftWrist, out var lw);
                sig.LiftLeft = sig.LiftLeftValid ? nose.Y - lw.Y : float.NegativeInfinity;
                sig.LiftRightValid = Visible(f, LandmarkIndex.RightWrist, out var rw);
                sig.LiftRight = sig.LiftRightValid ? nose.Y - rw.Y : float.NegativeInfinity;
                sig.OppositeLimitUpper = nose.Y - shoulderMidY;
                return shoulderW > 0f;
            }

            if (!Visible(f, LandmarkIndex.LeftHip, out var lh) || !Visible(f, LandmarkIndex.RightHip, out var rh)) return false;
            var hipMidY = (lh.Y + rh.Y) * 0.5f;
            var hipMidX = (lh.X + rh.X) * 0.5f;
            sig.Unit = Math.Abs(shoulderMidY - hipMidY);
            sig.BodyY = hipMidY;
            sig.BodyX = hipMidX;
            sig.LiftLeftValid = Visible(f, LandmarkIndex.LeftKnee, out var lk);
            sig.LiftLeft = sig.LiftLeftValid ? lh.Y - lk.Y : 0f;
            sig.LiftRightValid = Visible(f, LandmarkIndex.RightKnee, out var rk);
            sig.LiftRight = sig.LiftRightValid ? rh.Y - rk.Y : 0f;
            return sig.Unit > 0f;
        }

        private bool Visible(PoseFrame f, int index, out Landmark lm)
            => f.TryGet(index, out lm) && lm.Visibility >= s.MinVisibility;
    }
}
