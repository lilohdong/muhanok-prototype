#nullable enable
using Muhanok.Application.Pose;
using Muhanok.Application.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Muhanok.Presentation.Views
{
    /// 화면 구석의 작은 랜드마크 미리보기. 카메라 프레임(정규화 0~1)을 사각형에 그대로 매핑한다.
    /// MediaPipe y는 아래로 갈수록 커지므로 UI y로 바꿀 때만 1-y 한다 (판정 코드는 뒤집지 않는다).
    public sealed class PoseOverlayView : MonoBehaviour
    {
        private RectTransform box = null!;
        private RectTransform[] dots = null!;
        private Image frame = null!;
        private float size;

        public static PoseOverlayView Build(Transform canvas, PresentationProfile p)
        {
            var go = new GameObject("PoseOverlay", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var view = go.AddComponent<PoseOverlayView>();
            view.size = p.poseOverlaySize;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 16f);
            // 4:3 카메라 프레임
            rt.sizeDelta = new Vector2(p.poseOverlaySize, p.poseOverlaySize * 0.75f);
            view.box = rt;

            view.frame = go.AddComponent<Image>();
            view.frame.color = new Color(0f, 0f, 0f, 0.45f);

            view.dots = new RectTransform[LandmarkIndex.SlotCount];
            for (var i = 0; i < view.dots.Length; i++)
            {
                var d = new GameObject("lm" + LandmarkIndex.Sent[i], typeof(RectTransform));
                d.transform.SetParent(go.transform, false);
                var img = d.AddComponent<Image>();
                img.color = Color.green;
                var drt = d.GetComponent<RectTransform>();
                drt.anchorMin = Vector2.zero;
                drt.anchorMax = Vector2.zero;
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(6f, 6f);
                view.dots[i] = drt;
            }
            return view;
        }

        public void Refresh(IPoseStatus pose)
        {
            var f = pose.LastFrame;
            var w = box.sizeDelta.x;
            var h = box.sizeDelta.y;
            frame.color = !pose.IsReceiving ? new Color(0.5f, 0f, 0f, 0.5f)
                : !pose.IsTracking ? new Color(0.5f, 0.4f, 0f, 0.5f)
                : new Color(0f, 0f, 0f, 0.45f);

            for (var i = 0; i < dots.Length; i++)
            {
                var visible = f != null && f.TryGet(LandmarkIndex.Sent[i], out var lm) && lm.Visibility > 0.3f;
                dots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                f!.TryGet(LandmarkIndex.Sent[i], out lm);
                dots[i].anchoredPosition = new Vector2(Mathf.Clamp01(lm.X) * w, (1f - Mathf.Clamp01(lm.Y)) * h);
            }
        }
    }
}
