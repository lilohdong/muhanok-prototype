#nullable enable
namespace Muhanok.Application.Pose
{
    /// 어떤 신체 부위로 판정하는가. CLAUDE.md §1 카메라 제약 / §8.6.
    public enum BodyMode
    {
        /// 노트북 내장 캠: 머리~어깨. 골반 이하는 화면 밖. 기준 단위 = 어깨 너비.
        UpperBody,
        /// 전신 캠. 기준 단위 = 어깨-골반 거리(torso). §8.3 원안.
        FullBody,
    }

    /// PostureDetector가 쓰는 모든 숫자. 값의 출처는 TuningProfile(ScriptableObject)이고 여기는 순수 데이터 사본이다.
    /// 임계값은 전부 기준 단위(torso 또는 shoulderW)의 배수다. 절대 좌표 임계값은 없다.
    public sealed class DetectorSettings
    {
        public BodyMode Mode { get; }

        public float JumpThreshold { get; }
        /// FullBody: 무릎 들기 (hipY - kneeY) / torso
        public float KneeThreshold { get; }
        /// FullBody: 반대쪽 무릎은 KneeThreshold × 이 값 미만이어야 한다
        public float KneeOppositeFactor { get; }
        /// UpperBody: 한 손 들기 (noseY - wristY) / shoulderW
        public float ArmRaiseThreshold { get; }
        public float DuckThreshold { get; }
        public float StepThreshold { get; }

        /// 히스테리시스: 해제 임계값 = 진입 × 이 값
        public float ReleaseFactor { get; }
        /// 연속 N프레임 만족해야 인정
        public int ConfirmFrames { get; }
        public double CooldownSeconds { get; }

        /// 베이스라인 중앙값 창의 샘플 수 (30fps 기준 30 ≈ 1초)
        public int BaselineSampleCount { get; }
        /// 이만큼 쌓이기 전엔 판정하지 않는다
        public int BaselineMinSamples { get; }

        public float MinVisibility { get; }
        public double LostTimeoutSeconds { get; }

        /// true면 카메라가 사용자를 마주 본다(미러링 없음): 사용자의 왼쪽 이동 = 이미지 x 증가.
        public bool CameraFacesUser { get; }

        public DetectorSettings(
            BodyMode mode,
            float jumpThreshold,
            float kneeThreshold,
            float kneeOppositeFactor,
            float armRaiseThreshold,
            float duckThreshold,
            float stepThreshold,
            float releaseFactor,
            int confirmFrames,
            double cooldownSeconds,
            int baselineSampleCount,
            int baselineMinSamples,
            float minVisibility,
            double lostTimeoutSeconds,
            bool cameraFacesUser)
        {
            Mode = mode;
            JumpThreshold = jumpThreshold;
            KneeThreshold = kneeThreshold;
            KneeOppositeFactor = kneeOppositeFactor;
            ArmRaiseThreshold = armRaiseThreshold;
            DuckThreshold = duckThreshold;
            StepThreshold = stepThreshold;
            ReleaseFactor = releaseFactor;
            ConfirmFrames = confirmFrames < 1 ? 1 : confirmFrames;
            CooldownSeconds = cooldownSeconds;
            BaselineSampleCount = baselineSampleCount < 1 ? 1 : baselineSampleCount;
            BaselineMinSamples = baselineMinSamples < 1 ? 1 : baselineMinSamples;
            MinVisibility = minVisibility;
            LostTimeoutSeconds = lostTimeoutSeconds;
            CameraFacesUser = cameraFacesUser;
        }
    }
}
