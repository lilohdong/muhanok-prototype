#nullable enable
using System;
using Muhanok.Application.Ports;
using UnityEngine;

namespace Muhanok.Infrastructure
{
    public sealed class UnityClock : IClock
    {
        public float DeltaTime => Time.deltaTime;
        public double NowSeconds => Time.realtimeSinceStartupAsDouble;
        public double UnixNowSeconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    /// 지연 이동평균(지수 평활). HUD 표시용.
    public sealed class LatencyTelemetry : ITelemetrySink, ILatencyReadout
    {
        private readonly double smoothing;
        private double average;
        private bool hasSamples;

        /// smoothing: 새 샘플의 가중치(0~1). 값이 클수록 빠르게 따라간다.
        public LatencyTelemetry(double smoothing)
        {
            this.smoothing = Math.Clamp(smoothing, 0.0, 1.0);
        }

        public void RecordLatency(double milliseconds)
        {
            if (!hasSamples)
            {
                average = milliseconds;
                hasSamples = true;
                return;
            }
            average += (milliseconds - average) * smoothing;
        }

        public bool HasSamples => hasSamples;
        public double AverageMilliseconds => average;
    }
}
