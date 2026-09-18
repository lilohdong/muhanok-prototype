#nullable enable
using System.Collections.Generic;
using Muhanok.Application.Pose;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    /// 전신 모드(§8.3)도 같은 상태 기계로 돈다는 것을 확인한다. 전신 캠이 생기면 이쪽 케이스를 늘린다.
    public sealed class PostureDetectorFullBodyTests
    {
        private const float Fps = 30f;
        // 서 있는 자세: 어깨 y 0.30, 골반 y 0.55 → torso 0.25. 무릎 y 0.75, 발목 0.95
        private const float ShoulderY = 0.30f, HipY = 0.55f, KneeY = 0.75f, Torso = HipY - ShoulderY;

        private static DetectorSettings FullBodyDefaults() => new DetectorSettings(
            BodyMode.FullBody,
            jumpThreshold: 0.22f, kneeThreshold: 0.55f, kneeOppositeFactor: 0.6f, armRaiseThreshold: 0.20f,
            duckThreshold: 0.40f, stepThreshold: 0.45f,
            releaseFactor: 0.7f, confirmFrames: 2, cooldownSeconds: 0.3,
            baselineSampleCount: 30, baselineMinSamples: 10,
            minVisibility: 0.5f, lostTimeoutSeconds: 1.0, cameraFacesUser: true);

        private static PoseFrame Frame(long seq, float dy = 0f, float leftKneeLift = 0f, float rightKneeLift = 0f)
        {
            return new PoseFrameBuilder()
                .Set(LandmarkIndex.Nose, 0.5f, 0.15f + dy, 0.95f)
                .Set(LandmarkIndex.LeftShoulder, 0.40f, ShoulderY + dy, 0.95f)
                .Set(LandmarkIndex.RightShoulder, 0.60f, ShoulderY + dy, 0.95f)
                .Set(LandmarkIndex.LeftHip, 0.44f, HipY + dy, 0.9f)
                .Set(LandmarkIndex.RightHip, 0.56f, HipY + dy, 0.9f)
                .Set(LandmarkIndex.LeftKnee, 0.44f, KneeY + dy - leftKneeLift, 0.9f)
                .Set(LandmarkIndex.RightKnee, 0.56f, KneeY + dy - rightKneeLift, 0.9f)
                .Build(seq, seq / Fps);
        }

        private static List<PlayerAction> Feed(PostureDetector d, IEnumerable<PoseFrame> frames)
        {
            var scratch = new PlayerAction[8];
            var actions = new List<PlayerAction>();
            foreach (var f in frames)
            {
                var n = d.Step(f, scratch);
                for (var i = 0; i < n; i++) actions.Add(scratch[i]);
            }
            return actions;
        }

        private static IEnumerable<PoseFrame> Sequence(ref long seq, int frames, float dy = 0f, float leftKnee = 0f, float rightKnee = 0f)
        {
            var list = new List<PoseFrame>();
            for (var i = 0; i < frames; i++) list.Add(Frame(seq++, dy, leftKnee, rightKnee));
            return list;
        }

        [Test]
        public void FullBody_Jump_UsesHipRise()
        {
            var d = new PostureDetector(FullBodyDefaults());
            long seq = 0;
            var actions = new List<PlayerAction>();
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            actions.AddRange(Feed(d, Sequence(ref seq, 6, dy: -0.4f * Torso)));   // 골반이 0.4 torso 뜸 (> 0.22)
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            Assert.That(actions, Is.EqualTo(new[] { PlayerAction.Jump }));
        }

        [Test]
        public void FullBody_KneeRaise_OneSideOnly()
        {
            var d = new PostureDetector(FullBodyDefaults());
            long seq = 0;
            var actions = new List<PlayerAction>();
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            // 왼 무릎을 골반 위까지: kneeY = 0.75 - 0.36 = 0.39 → (hipY - kneeY)/torso = 0.16/0.25 = 0.64 > 0.55
            actions.AddRange(Feed(d, Sequence(ref seq, 8, leftKnee: 0.36f)));
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            Assert.That(actions, Is.EqualTo(new[] { PlayerAction.KneeRaise }));

            // 양쪽 다 들면(앉는 동작 등) 무릎 들기가 아니다
            actions.AddRange(Feed(d, Sequence(ref seq, 8, leftKnee: 0.36f, rightKnee: 0.36f)));
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            Assert.That(actions.Count, Is.EqualTo(1));
        }

        [Test]
        public void FullBody_Duck_UsesNoseDropInTorsoUnits()
        {
            var d = new PostureDetector(FullBodyDefaults());
            long seq = 0;
            var actions = new List<PlayerAction>();
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            actions.AddRange(Feed(d, Sequence(ref seq, 10, dy: 0.6f * Torso)));   // 0.6 > 0.40
            actions.AddRange(Feed(d, Sequence(ref seq, 30)));
            Assert.That(actions, Is.EqualTo(new[] { PlayerAction.Duck }));
        }
    }
}
