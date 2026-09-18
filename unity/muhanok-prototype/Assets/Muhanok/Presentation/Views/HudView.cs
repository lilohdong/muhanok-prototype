#nullable enable
using System.Text;
using Muhanok.Application.Ports;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Muhanok.Presentation.Views
{
    /// 최소 HUD: 점수/거리/코인/교도관 거리/지연/포즈 상태. uGUI 레거시 Text로 런타임 생성.
    public sealed class HudView : MonoBehaviour
    {
        private PresentationProfile p = null!;
        private ILatencyReadout latency = null!;
        private IPoseStatus pose = null!;

        private Text stats = null!;
        private Text center = null!;
        private Text hint = null!;
        private PoseOverlayView? overlay;

        private readonly StringBuilder sb = new StringBuilder(256);
        private RunState? state;
        private string inputLabel = "";
        private float refreshRemaining;

        public void Construct(PresentationProfile profile, ILatencyReadout latencyReadout, IPoseStatus poseStatus, string inputSourceLabel)
        {
            p = profile;
            latency = latencyReadout;
            pose = poseStatus;
            inputLabel = inputSourceLabel;
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            gameObject.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stats = MakeText("Stats", font, p.hudFontSize, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(520f, 300f));
            center = MakeText("Center", font, p.hudFontSize * 2, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 200f));
            hint = MakeText("Hint", font, p.hudFontSize - 4, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(900f, 60f));
            hint.text = "Space=Jump  W=KneeRaise  S=Duck  A/D=Step   (input: " + inputLabel + ")";

            if (p.showPoseOverlay && pose.IsEnabled)
                overlay = PoseOverlayView.Build(transform, p);
        }

        private Text MakeText(string name, Font font, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = p.hudColor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public void Apply(RunState s)
        {
            state = s;
        }

        public void ShowGameOver(int score)
        {
            center.text = "CAUGHT!\nScore " + score + "\n\n(Stop and Play again to retry)";
        }

        private void Update()
        {
            refreshRemaining -= Time.deltaTime;
            if (refreshRemaining > 0f) return;
            refreshRemaining = p.hudRefreshSeconds;
            Refresh();
            overlay?.Refresh(pose);
        }

        private void Refresh()
        {
            if (state == null) return;
            sb.Clear();
            sb.Append("Score  ").Append(state.Score).Append('\n');
            sb.Append("Dist   ").Append(state.DistanceTravelled.ToString("F0")).Append(" m\n");
            sb.Append("Speed  ").Append(state.ForwardSpeed.ToString("F1")).Append(" m/s\n");
            sb.Append("Coins  ").Append(state.Coins).Append('\n');
            sb.Append("Gap    ").Append(state.ChaserGap.ToString("F1")).Append(" m  (stumbles ").Append(state.Stumbles).Append(")\n");
            sb.Append("Lane   ").Append(state.CurrentLane).Append("   Posture ").Append(state.Posture).Append('\n');

            sb.Append("Latency ");
            if (latency.HasSamples) sb.Append(latency.AverageMilliseconds.ToString("F0")).Append(" ms");
            else sb.Append("--");
            sb.Append('\n');

            if (pose.IsEnabled)
            {
                sb.Append("Pose   ");
                if (!pose.IsReceiving) sb.Append("NO SIGNAL (run `uv run pose live`)");
                else if (!pose.IsTracking) sb.Append("LOST — step back into frame");
                else sb.Append(pose.DetectedPosture).Append(" / zone ").Append(pose.DetectedZone);
                sb.Append('\n');
                sb.Append("  jump ").Append(pose.JumpRatio.ToString("F2"))
                  .Append("  lift ").Append(pose.LiftRatio.ToString("F2"))
                  .Append("  duck ").Append(pose.DuckRatio.ToString("F2"))
                  .Append("  step ").Append(pose.StepRatio.ToString("F2"));
            }
            stats.text = sb.ToString();
        }
    }
}
