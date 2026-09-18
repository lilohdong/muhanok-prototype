#nullable enable
namespace Muhanok.Domain
{
    /// "이 자세로 이 장애물을 통과할 수 있는가" — 게임 규칙의 핵심. 순수 함수.
    /// 규칙 표는 CLAUDE.md §5. 이 표가 곧 EditMode 테스트 케이스다.
    public static class ClearanceRule
    {
        /// 부딪히지 않고 통과하면 true.
        public static bool Clears(
            ObstacleKind kind,
            Lane obstacleLane,
            Lane playerLane,
            PostureState posture)
        {
            if (obstacleLane != playerLane) return true;

            switch (kind)
            {
                case ObstacleKind.Stairs:
                    return posture == PostureState.KneeRaised || posture == PostureState.Airborne;
                case ObstacleKind.Barricade:
                    return posture == PostureState.Airborne;
                case ObstacleKind.Cage:
                    return posture == PostureState.Ducking;
                case ObstacleKind.PoliceCar:
                    return false;
                default:
                    return false;
            }
        }
    }
}
