#nullable enable
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    /// CLAUDE.md §5 규칙 표 전수: 장애물 4 × 자세 4 × 레인 일치/불일치 = 32 케이스.
    public sealed class ClearanceRuleTests
    {
        private static readonly PostureState[] AllPostures =
            { PostureState.Grounded, PostureState.Airborne, PostureState.KneeRaised, PostureState.Ducking };

        [Test]
        public void DifferentLane_AlwaysClears(
            [Values] ObstacleKind kind,
            [Values] PostureState posture)
        {
            Assert.That(ClearanceRule.Clears(kind, Lane.Left, Lane.Center, posture), Is.True);
            Assert.That(ClearanceRule.Clears(kind, Lane.Right, Lane.Left, posture), Is.True);
        }

        [TestCase(PostureState.Grounded, false)]
        [TestCase(PostureState.Airborne, true)]
        [TestCase(PostureState.KneeRaised, true)]
        [TestCase(PostureState.Ducking, false)]
        public void Stairs_SameLane(PostureState posture, bool expected)
            => Assert.That(ClearanceRule.Clears(ObstacleKind.Stairs, Lane.Center, Lane.Center, posture), Is.EqualTo(expected));

        [TestCase(PostureState.Grounded, false)]
        [TestCase(PostureState.Airborne, true)]
        [TestCase(PostureState.KneeRaised, false)]
        [TestCase(PostureState.Ducking, false)]
        public void Barricade_SameLane(PostureState posture, bool expected)
            => Assert.That(ClearanceRule.Clears(ObstacleKind.Barricade, Lane.Center, Lane.Center, posture), Is.EqualTo(expected));

        [TestCase(PostureState.Grounded, false)]
        [TestCase(PostureState.Airborne, false)]
        [TestCase(PostureState.KneeRaised, false)]
        [TestCase(PostureState.Ducking, true)]
        public void Cage_SameLane(PostureState posture, bool expected)
            => Assert.That(ClearanceRule.Clears(ObstacleKind.Cage, Lane.Center, Lane.Center, posture), Is.EqualTo(expected));

        [Test]
        public void PoliceCar_SameLane_NeverClears()
        {
            foreach (var posture in AllPostures)
                Assert.That(ClearanceRule.Clears(ObstacleKind.PoliceCar, Lane.Right, Lane.Right, posture), Is.False, posture.ToString());
        }

        [Test]
        public void SameLane_MatrixMatchesSpecTable()
        {
            // 행: Stairs, Barricade, Cage, PoliceCar / 열: Grounded, Airborne, KneeRaised, Ducking
            bool[,] expected =
            {
                { false, true,  true,  false },
                { false, true,  false, false },
                { false, false, false, true  },
                { false, false, false, false },
            };
            var kinds = new[] { ObstacleKind.Stairs, ObstacleKind.Barricade, ObstacleKind.Cage, ObstacleKind.PoliceCar };
            for (var k = 0; k < kinds.Length; k++)
            for (var p = 0; p < AllPostures.Length; p++)
                Assert.That(ClearanceRule.Clears(kinds[k], Lane.Left, Lane.Left, AllPostures[p]),
                    Is.EqualTo(expected[k, p]), $"{kinds[k]} / {AllPostures[p]}");
        }
    }
}
