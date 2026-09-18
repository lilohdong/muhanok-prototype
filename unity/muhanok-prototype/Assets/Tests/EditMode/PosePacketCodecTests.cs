#nullable enable
using Muhanok.Application.Pose;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class PosePacketCodecTests
    {
        private const string Sample =
            "{\"v\": 1, \"seq\": 1042, \"t\": 1758182400.123, \"ok\": true, \"lm\": {" +
            "\"0\": [0.501, 0.180, 0.92], \"11\": [0.430, 0.310, 0.95], \"12\": [0.572, 0.309, 0.95]," +
            "\"15\": [0.40, 0.55, 0.80], \"23\": [0.448, 0.560, 0.93]}}";

        [Test]
        public void Decodes_SpecSample()
        {
            var r = PosePacketCodec.TryDecode(Sample, out var frame);
            Assert.That(r, Is.EqualTo(PosePacketCodec.Result.Ok));
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame!.Seq, Is.EqualTo(1042));
            Assert.That(frame.Time, Is.EqualTo(1758182400.123).Within(1e-6));
            Assert.That(frame.Ok, Is.True);
            Assert.That(frame.Has(LandmarkIndex.Nose), Is.True);
            Assert.That(frame.Get(LandmarkIndex.LeftShoulder).X, Is.EqualTo(0.430f).Within(1e-5f));
            Assert.That(frame.Get(LandmarkIndex.LeftWrist).Visibility, Is.EqualTo(0.80f).Within(1e-5f));
            Assert.That(frame.Has(LandmarkIndex.RightWrist), Is.False);
        }

        [Test]
        public void Decodes_LostFrame_WithoutLandmarks()
        {
            var r = PosePacketCodec.TryDecode("{\"v\":1,\"seq\":7,\"t\":12.5,\"ok\":false}", out var frame);
            Assert.That(r, Is.EqualTo(PosePacketCodec.Result.Ok));
            Assert.That(frame!.Ok, Is.False);
            Assert.That(frame.Has(LandmarkIndex.Nose), Is.False);
        }

        [Test]
        public void RejectsWrongVersion()
        {
            var r = PosePacketCodec.TryDecode("{\"v\":2,\"seq\":1,\"t\":0,\"ok\":false}", out _);
            Assert.That(r, Is.EqualTo(PosePacketCodec.Result.VersionMismatch));
        }

        [Test]
        public void RejectsGarbage()
        {
            Assert.That(PosePacketCodec.TryDecode("not json", out _), Is.EqualTo(PosePacketCodec.Result.Malformed));
            Assert.That(PosePacketCodec.TryDecode("{\"v\":1,\"seq\":", out _), Is.EqualTo(PosePacketCodec.Result.Malformed));
        }

        [Test]
        public void IgnoresUnknownFields()
        {
            var r = PosePacketCodec.TryDecode(
                "{\"v\":1,\"extra\":{\"a\":[1,2,{\"b\":\"}\"}]},\"seq\":3,\"t\":1.0,\"ok\":true,\"lm\":{\"0\":[0.5,0.2,0.9]},\"tail\":\"x\"}",
                out var frame);
            Assert.That(r, Is.EqualTo(PosePacketCodec.Result.Ok));
            Assert.That(frame!.Seq, Is.EqualTo(3));
            Assert.That(frame.Has(LandmarkIndex.Nose), Is.True);
        }

        [Test]
        public void RoundTrips()
        {
            var original = new PoseFrameBuilder()
                .Set(LandmarkIndex.Nose, 0.5f, 0.2f, 0.9f)
                .Set(LandmarkIndex.LeftShoulder, 0.4f, 0.3f, 0.95f)
                .Set(LandmarkIndex.RightShoulder, 0.6f, 0.3f, 0.95f)
                .Build(99, 1234.5);

            var line = PosePacketCodec.Encode(original);
            var r = PosePacketCodec.TryDecode(line, out var decoded);
            Assert.That(r, Is.EqualTo(PosePacketCodec.Result.Ok));
            Assert.That(decoded!.Seq, Is.EqualTo(99));
            Assert.That(decoded.Get(LandmarkIndex.RightShoulder).X, Is.EqualTo(0.6f).Within(1e-3f));
            Assert.That(decoded.Has(LandmarkIndex.LeftHip), Is.False);
        }
    }
}
