#nullable enable
using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Application.Ports;
using UnityEngine;

namespace Muhanok.Presentation
{
    /// Unity의 Update를 Application에 넘겨주는 얇은 다리. 입력 소스 폴링 → 세션 틱. 규칙 없음.
    public sealed class GameLoopBehaviour : MonoBehaviour
    {
        private RunSession session = null!;
        private IClock clock = null!;
        private IReadOnlyList<IPerFrame> perFrame = new List<IPerFrame>();
        private bool started;

        public void Construct(RunSession session, IClock clock, IReadOnlyList<IPerFrame> perFrame)
        {
            this.session = session;
            this.clock = clock;
            this.perFrame = perFrame;
        }

        private void Update()
        {
            if (session == null) return;
            if (!started)
            {
                started = true;
                session.Start();
            }

            var dt = clock.DeltaTime;
            for (var i = 0; i < perFrame.Count; i++) perFrame[i].OnFrame(dt);
            session.Tick(dt);
        }
    }
}
