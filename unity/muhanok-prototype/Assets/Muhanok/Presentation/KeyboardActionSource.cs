#nullable enable
using System;
using Muhanok.Application.Ports;
using Muhanok.Domain;
using UnityEngine.InputSystem;

namespace Muhanok.Presentation
{
    /// 디버그용 키보드 입력. 포즈 입력과 같은 이벤트만 낸다 — 게임은 둘을 구분하지 못한다.
    /// Space=Jump, W=KneeRaise, S=Duck, A/D=StepLeft/Right (§14)
    public sealed class KeyboardActionSource : IPlayerActionSource, IPerFrame
    {
        public event Action<PlayerAction>? ActionDetected;

        public void OnFrame(float deltaSeconds)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame) ActionDetected?.Invoke(PlayerAction.Jump);
            if (kb.wKey.wasPressedThisFrame) ActionDetected?.Invoke(PlayerAction.KneeRaise);
            if (kb.sKey.wasPressedThisFrame) ActionDetected?.Invoke(PlayerAction.Duck);
            if (kb.aKey.wasPressedThisFrame) ActionDetected?.Invoke(PlayerAction.StepLeft);
            if (kb.dKey.wasPressedThisFrame) ActionDetected?.Invoke(PlayerAction.StepRight);
        }
    }
}
