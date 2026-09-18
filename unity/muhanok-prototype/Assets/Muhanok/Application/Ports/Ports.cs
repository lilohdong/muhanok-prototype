#nullable enable
using System;
using Muhanok.Domain;

namespace Muhanok.Application.Ports
{
    /// 플레이어 동작의 출처. 키보드든 카메라든 여기 뒤에 숨는다. 게임 로직은 어느 쪽이 꽂혔는지 모른다.
    public interface IPlayerActionSource
    {
        event Action<PlayerAction> ActionDetected;
    }

    /// 구간 배치도 생성기. 시드를 받아 결정적으로 동작해야 한다.
    public interface ISegmentLayoutGenerator
    {
        SegmentLayout Next(int segmentIndex);
    }

    /// 게임 상태를 화면에 반영하는 출력 포트.
    public interface IRunPresenter
    {
        void OnStateChanged(RunState state);
        void OnPlayerStumbled();
        void OnCoinCollected(int total);
        void OnRunEnded(int finalScore);
    }

    /// 트랙(구간) 생성/회수를 화면에 알리는 출력 포트. 구간 오브젝트 풀링은 이 이벤트로 돌아간다.
    public interface ITrackPresenter
    {
        void OnSegmentSpawned(TrackSegment segment);
        void OnSegmentRetired(TrackSegment segment);
        void OnCoinTaken(TrackSegment segment, int coinIndex);
    }

    public interface IClock
    {
        float DeltaTime { get; }
        double NowSeconds { get; }
        /// Unix epoch 초. Python 패킷의 t와 같은 시간축. 지연 측정에만 쓴다.
        double UnixNowSeconds { get; }
    }

    /// 지연 측정 등 계측값 수집.
    public interface ITelemetrySink
    {
        void RecordLatency(double milliseconds);
    }

    /// HUD가 읽는 지연 이동평균.
    public interface ILatencyReadout
    {
        bool HasSamples { get; }
        double AverageMilliseconds { get; }
    }

    /// HUD·디버그 오버레이가 읽는 포즈 입력 상태. 키보드 모드에서는 Null 구현이 꽂힌다.
    public interface IPoseStatus
    {
        bool IsEnabled { get; }
        bool IsReceiving { get; }
        bool IsTracking { get; }
        PostureState DetectedPosture { get; }
        Lane DetectedZone { get; }
        Pose.PoseFrame? LastFrame { get; }
        float JumpRatio { get; }
        float LiftRatio { get; }
        float DuckRatio { get; }
        float StepRatio { get; }
    }

    /// 메인 스레드에서 매 프레임 한 번 호출된다. 입력 소스 폴링 등.
    public interface IPerFrame
    {
        void OnFrame(float deltaSeconds);
    }
}
