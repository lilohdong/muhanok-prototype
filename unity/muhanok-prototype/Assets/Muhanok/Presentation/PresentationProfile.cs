#nullable enable
using UnityEngine;

namespace Muhanok.Presentation
{
    /// 보여주기에 쓰는 숫자와 색. 게임 규칙과 무관한 값만 여기 둔다.
    [CreateAssetMenu(menuName = "Muhanok/Presentation Profile", fileName = "PresentationProfile")]
    public sealed class PresentationProfile : ScriptableObject
    {
        [Header("World")]
        public float laneWidth = 2.0f;
        public float floorThickness = 0.2f;
        public Color floorColorA = new Color(0.32f, 0.32f, 0.36f);
        public Color floorColorB = new Color(0.27f, 0.27f, 0.31f);

        [Header("Player")]
        public Color playerColor = new Color(0.95f, 0.55f, 0.10f);
        public Color stumbleFlashColor = new Color(0.90f, 0.15f, 0.15f);
        public float stumbleFlashSeconds = 0.35f;
        public float laneLerpSpeed = 12f;
        public float jumpHeight = 1.6f;
        public float duckScaleY = 0.5f;
        public float kneeRaiseLift = 0.45f;
        public float kneeRaiseTilt = 20f;
        public float postureLerpSpeed = 14f;

        [Header("Chaser")]
        public Color chaserColor = new Color(0.20f, 0.30f, 0.85f);
        public float chaserLerpSpeed = 6f;

        [Header("Obstacles")]
        public Color stairsColor = new Color(0.55f, 0.55f, 0.55f);
        public Color barricadeColor = new Color(0.85f, 0.75f, 0.20f);
        public Color cageColor = new Color(0.30f, 0.30f, 0.30f);
        public Color policeCarColor = new Color(0.10f, 0.10f, 0.10f);
        public Color coinColor = new Color(1.0f, 0.85f, 0.10f);
        public float coinSpinDegPerSec = 180f;
        public float cageBarHeight = 1.2f;   // 이 높이 아래로 엎드려 지나간다

        [Header("Camera")]
        public Vector3 cameraOffset = new Vector3(0f, 4f, -7f);
        public float cameraPitchDeg = 15f;
        public Color backgroundColor = new Color(0.55f, 0.65f, 0.80f);
        public Color lightColor = Color.white;
        public float lightIntensity = 1.0f;
        public Vector3 lightEuler = new Vector3(50f, -30f, 0f);

        [Header("HUD")]
        public float hudRefreshSeconds = 0.1f;
        public int hudFontSize = 20;
        public Color hudColor = Color.white;
        public bool showPoseOverlay = true;
        public float poseOverlaySize = 160f;
    }
}
