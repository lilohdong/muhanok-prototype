#nullable enable
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Application.Ports;
using Muhanok.Presentation.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Muhanok.Presentation
{
    /// Unity의 Update를 Application에 넘겨주는 얇은 다리. 입력 소스 폴링 → 세션 틱. 규칙 없음.
    /// 게임이 끝나면 R 키 또는 일정 시간 뒤 씬을 다시 로드한다 — 모든 것이 런타임 생성이라 그게 곧 리셋이다.
    public sealed class GameLoopBehaviour : MonoBehaviour
    {
        private RunSession session = null!;
        private IClock clock = null!;
        private IReadOnlyList<IPerFrame> perFrame = new List<IPerFrame>();
        private IPoseStatus pose = null!;
        private HudView hud = null!;
        private float restartAfterSeconds;
        private float overElapsed;
        private bool started;
        private bool restarting;

        public void Construct(RunSession session, IClock clock, IReadOnlyList<IPerFrame> perFrame, IPoseStatus pose, HudView hud, float restartAfterSeconds)
        {
            this.session = session;
            this.clock = clock;
            this.perFrame = perFrame;
            this.pose = pose;
            this.hud = hud;
            this.restartAfterSeconds = restartAfterSeconds;
        }

        private void Update()
        {
            if (session == null) return;
            var dt = clock.DeltaTime;
            for (var i = 0; i < perFrame.Count; i++) perFrame[i].OnFrame(dt);

            // 캘리브레이션 게이트: 포즈 모드는 머리+어깨가 잡혀 베이스라인이 생길 때까지 출발하지 않는다.
            if (!started)
            {
                var ready = !pose.IsEnabled || pose.IsTracking;
                hud.ShowCalibration(ready ? null : pose.IsReceiving ? HudView.CalibrationInFrame : HudView.CalibrationNoSignal);
                if (!ready) return;
                started = true;
                session.Start();
            }

            session.Tick(dt);

            if (session.State.IsOver) HandleGameOver(dt);
        }

        private void HandleGameOver(float dt)
        {
            if (restarting) return;
            overElapsed += dt;
            var kb = Keyboard.current;
            var pressedR = kb != null && kb.rKey.wasPressedThisFrame;
            var timedOut = restartAfterSeconds > 0f && overElapsed >= restartAfterSeconds;
            if (!pressedR && !timedOut) return;

            restarting = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
