#nullable enable
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Views
{
    /// 주인공 캡슐. 상태를 받아 보간해서 그리기만 한다. 규칙 판단 없음.
    public sealed class PlayerView : MonoBehaviour
    {
        private PresentationProfile p = null!;
        private PrimitiveFactory factory = null!;
        private Transform body = null!;
        private Renderer bodyRenderer = null!;

        private Lane targetLane = Lane.Center;
        private PostureState posture = PostureState.Grounded;
        private float postureDuration = 1f;
        private float postureElapsed;
        private float flashRemaining;

        public static PlayerView Build(PrimitiveFactory factory, PresentationProfile p, float postureDuration)
        {
            var root = PrimitiveFactory.Empty("Player", null);
            var view = root.AddComponent<PlayerView>();
            view.p = p;
            view.factory = factory;
            view.postureDuration = Mathf.Max(0.01f, postureDuration);
            var body = factory.Create(PrimitiveType.Capsule, "Body", p.playerColor, root.transform,
                new Vector3(0f, 1f, 0f), Vector3.one);
            view.body = body.transform;
            view.bodyRenderer = body.GetComponent<Renderer>();
            // 앞을 보는 표시(코). 어느 쪽이 앞인지 한눈에 보이게.
            factory.Create(PrimitiveType.Cube, "Nose", p.playerColor, body.transform,
                new Vector3(0f, 0.6f, 0.45f), new Vector3(0.3f, 0.2f, 0.3f));
            return view;
        }

        public void Apply(RunState state)
        {
            targetLane = state.CurrentLane;
            if (state.Posture != posture)
            {
                posture = state.Posture;
                postureElapsed = 0f;
            }
        }

        public void Flash()
        {
            flashRemaining = p.stumbleFlashSeconds;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            postureElapsed += dt;

            var pos = transform.position;
            pos.x = Mathf.Lerp(pos.x, (int)targetLane * p.laneWidth, 1f - Mathf.Exp(-p.laneLerpSpeed * dt));
            transform.position = pos;

            ApplyPosture(dt);
            ApplyFlash(dt);
        }

        private void ApplyPosture(float dt)
        {
            var t = Mathf.Clamp01(postureElapsed / postureDuration);
            var targetY = 1f;
            var targetScaleY = 1f;
            var targetTilt = 0f;

            switch (posture)
            {
                case PostureState.Airborne:
                    targetY = 1f + p.jumpHeight * Mathf.Sin(t * Mathf.PI);
                    break;
                case PostureState.Ducking:
                    targetScaleY = p.duckScaleY;
                    targetY = p.duckScaleY;
                    break;
                case PostureState.KneeRaised:
                    targetY = 1f + p.kneeRaiseLift * Mathf.Sin(t * Mathf.PI);
                    targetTilt = -p.kneeRaiseTilt * Mathf.Sin(t * Mathf.PI);
                    break;
            }

            var k = 1f - Mathf.Exp(-p.postureLerpSpeed * dt);
            var lp = body.localPosition;
            lp.y = posture == PostureState.Airborne || posture == PostureState.KneeRaised ? targetY : Mathf.Lerp(lp.y, targetY, k);
            body.localPosition = lp;
            var s = body.localScale;
            s.y = Mathf.Lerp(s.y, targetScaleY, k);
            body.localScale = s;
            body.localRotation = Quaternion.Euler(targetTilt, 0f, 0f);
        }

        private void ApplyFlash(float dt)
        {
            if (flashRemaining <= 0f) return;
            flashRemaining -= dt;
            bodyRenderer.sharedMaterial = factory.MaterialFor(flashRemaining > 0f ? p.stumbleFlashColor : p.playerColor);
        }
    }

    /// 뒤에서 쫓아오는 교도관. ChaserGap만큼 뒤에 있다.
    public sealed class ChaserView : MonoBehaviour
    {
        private PresentationProfile p = null!;
        private float targetGap;

        public static ChaserView Build(PrimitiveFactory factory, PresentationProfile p)
        {
            var root = PrimitiveFactory.Empty("Chaser", null);
            var view = root.AddComponent<ChaserView>();
            view.p = p;
            factory.Create(PrimitiveType.Capsule, "Body", p.chaserColor, root.transform,
                new Vector3(0f, 1f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            factory.Create(PrimitiveType.Cube, "Cap", p.chaserColor, root.transform,
                new Vector3(0f, 2.15f, 0.1f), new Vector3(0.7f, 0.15f, 0.9f));
            return view;
        }

        public void Apply(RunState state)
        {
            targetGap = Mathf.Max(0f, state.ChaserGap);
        }

        private void Update()
        {
            var pos = transform.position;
            pos.z = Mathf.Lerp(pos.z, -targetGap, 1f - Mathf.Exp(-p.chaserLerpSpeed * Time.deltaTime));
            transform.position = pos;
        }
    }
}
