#nullable enable
namespace Muhanok.Domain
{
    public enum Lane { Left = -1, Center = 0, Right = 1 }

    public enum PlayerAction { Jump, KneeRaise, Duck, StepLeft, StepRight }

    public enum ObstacleKind { Stairs, Barricade, Cage, PoliceCar }

    /// 플레이어의 세로 자세 상태.
    public enum PostureState { Grounded, Airborne, KneeRaised, Ducking }

    public static class LaneRules
    {
        /// 레인 밖으로 나가는 이동은 무시한다. 벽에 부딪히는 개념이 없다.
        public static Lane Shift(Lane lane, int direction)
        {
            var next = (int)lane + (direction < 0 ? -1 : 1);
            if (next < (int)Lane.Left) return Lane.Left;
            if (next > (int)Lane.Right) return Lane.Right;
            return (Lane)next;
        }
    }
}
