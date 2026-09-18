#nullable enable
using Muhanok.Application.Pose;
using UnityEngine;

namespace Muhanok.Infrastructure
{
    /// 동작 판정 임계값(§8). 인스펙터에서 실시간 튜닝한다. 코드에 숫자를 박는 대신 여기 모은다.
    /// 초기값의 근거는 CLAUDE.md §8.3(전신) / §8.6(상반신).
    [CreateAssetMenu(menuName = "Muhanok/Tuning Profile", fileName = "TuningProfile")]
    public sealed class TuningProfile : ScriptableObject
    {
        [Header("Body")]
        [Tooltip("UpperBody: 노트북 캠(머리~어깨). FullBody: 전신 캠.")]
        public BodyMode bodyMode = BodyMode.UpperBody;
        [Tooltip("true = 카메라가 사용자를 마주 봄(미러링 없음). 사용자의 왼쪽 이동 = 이미지 x 증가")]
        public bool cameraFacesUser = true;

        [Header("Thresholds (× unit)  unit = shoulderW(UpperBody) / torso(FullBody)")]
        [Range(0.05f, 2f)] public float jumpThreshold = 0.45f;
        [Range(0.05f, 2f)] public float kneeThreshold = 0.55f;
        [Range(0.1f, 1f)] public float kneeOppositeFactor = 0.6f;
        [Range(0.0f, 2f)] public float armRaiseThreshold = 0.20f;
        [Range(0.05f, 2f)] public float duckThreshold = 0.80f;
        [Range(0.05f, 2f)] public float stepThreshold = 0.30f;
        [Tooltip("사이드 스텝 기준점: 0 = 어깨 중심만, 1 = 코만. 기울이기로 조작하려면 올린다")]
        [Range(0f, 1f)] public float stepHeadWeight = 0.6f;

        [Header("Stabilizers (§8.4)")]
        [Range(0.3f, 1f)] public float releaseFactor = 0.7f;
        [Range(1, 10)] public int confirmFrames = 2;
        [Range(0f, 2f)] public float cooldownSeconds = 0.3f;

        [Header("Baseline (§8.2)")]
        [Range(5, 120)] public int baselineSampleCount = 30;
        [Range(1, 60)] public int baselineMinSamples = 10;

        [Header("Loss handling")]
        [Range(0f, 1f)] public float minVisibility = 0.5f;
        [Range(0.1f, 5f)] public float lostTimeoutSeconds = 1.0f;

        /// 전신 모드 기본값으로 되돌릴 때의 참고값(§8.3): jump 0.22, knee 0.55, duck 0.40, step 0.45.
        public DetectorSettings ToSettings() => new DetectorSettings(
            bodyMode,
            jumpThreshold,
            kneeThreshold,
            kneeOppositeFactor,
            armRaiseThreshold,
            duckThreshold,
            stepThreshold,
            stepHeadWeight,
            releaseFactor,
            confirmFrames,
            cooldownSeconds,
            baselineSampleCount,
            baselineMinSamples,
            minVisibility,
            lostTimeoutSeconds,
            cameraFacesUser);
    }
}
