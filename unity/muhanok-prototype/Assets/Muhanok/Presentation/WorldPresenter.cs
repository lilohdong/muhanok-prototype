#nullable enable
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Application.Ports;
using Muhanok.Domain;
using Muhanok.Presentation.Views;
using UnityEngine;

namespace Muhanok.Presentation
{
    /// Application의 출력 포트 구현. 상태를 받아 월드/HUD에 반영한다. 게임 규칙은 판단하지 않는다.
    /// 월드는 플레이어가 아니라 트랙이 움직인다: 플레이어는 z=0에 고정, worldRoot.z = -DistanceTravelled.
    public sealed class WorldPresenter : IRunPresenter, ITrackPresenter
    {
        private readonly PresentationProfile p;
        private readonly HudView hud;
        private readonly Transform worldRoot;
        private readonly ViewPools pools;
        private readonly PlayerView player;
        private readonly ChaserView chaser;
        private readonly Dictionary<int, SegmentView> segments = new Dictionary<int, SegmentView>();

        public WorldPresenter(PresentationProfile profile, HudView hud, RunTuning tuning)
        {
            p = profile;
            this.hud = hud;

            var factory = new PrimitiveFactory();
            worldRoot = PrimitiveFactory.Empty("World", null).transform;
            pools = new ViewPools(factory, p, worldRoot);
            player = PlayerView.Build(factory, p, tuning.PostureDuration);
            chaser = ChaserView.Build(factory, p);
            SetupCameraAndLight();
        }

        private void SetupCameraAndLight()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                cam = go.GetComponent<Camera>();
            }
            cam.transform.position = p.cameraOffset;
            cam.transform.rotation = Quaternion.Euler(p.cameraPitchDeg, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = p.backgroundColor;

            var light = new GameObject("Muhanok Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = p.lightColor;
            light.intensity = p.lightIntensity;
            light.transform.rotation = Quaternion.Euler(p.lightEuler);
        }

        // ── IRunPresenter ──

        public void OnStateChanged(RunState state)
        {
            var pos = worldRoot.position;
            pos.z = -state.DistanceTravelled;
            worldRoot.position = pos;

            player.Apply(state);
            chaser.Apply(state);
            hud.Apply(state);
        }

        public void OnPlayerStumbled() => player.Flash();

        public void OnCoinCollected(int total) { }

        public void OnRunEnded(int finalScore) => hud.ShowGameOver(finalScore);

        // ── ITrackPresenter ──

        public void OnSegmentSpawned(TrackSegment segment)
        {
            var view = pools.GetSegment();
            view.Show(segment, pools, worldRoot);
            segments[segment.Index] = view;
        }

        public void OnSegmentRetired(TrackSegment segment)
        {
            if (!segments.TryGetValue(segment.Index, out var view)) return;
            segments.Remove(segment.Index);
            view.Clear(pools);
            pools.Release(view);
        }

        public void OnCoinTaken(TrackSegment segment, int coinIndex)
        {
            if (segments.TryGetValue(segment.Index, out var view)) view.HideCoin(coinIndex, pools);
        }
    }
}
