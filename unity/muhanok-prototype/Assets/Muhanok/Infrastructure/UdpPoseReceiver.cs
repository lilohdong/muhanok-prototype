#nullable enable
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Muhanok.Application.Pose;

namespace Muhanok.Infrastructure
{
    /// UDP 수신 스레드. 최신 패킷 하나만 슬롯에 덮어쓴다. 큐를 쌓지 않는다 — 쌓이면 지연이 누적된다.
    /// 이 스레드에서는 Unity API를 절대 호출하지 않는다.
    public sealed class UdpPoseReceiver : IDisposable
    {
        public sealed class Received
        {
            public readonly PoseFrame Frame;
            /// 수신 시각(Unix 초). 패킷의 t와 빼서 지연을 구한다.
            public readonly double ReceivedAtUnixSeconds;

            public Received(PoseFrame frame, double receivedAtUnixSeconds)
            {
                Frame = frame;
                ReceivedAtUnixSeconds = receivedAtUnixSeconds;
            }
        }

        private readonly UdpClient client;
        private readonly Thread thread;
        private volatile bool running;
        private Received? latest;   // Volatile.Write / Interlocked.Exchange로만 접근
        private long lastSeq = -1;

        private int dropped;
        private int malformed;
        private int versionMismatch;

        public int Dropped => dropped;
        public int Malformed => malformed;
        public int VersionMismatch => versionMismatch;
        public int Port { get; }

        public UdpPoseReceiver(int port)
        {
            Port = port;
            client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "UdpPoseReceiver" };
            thread.Start();
        }

        /// 메인 스레드용. 새 패킷이 있으면 한 번만 돌려주고 슬롯을 비운다.
        public Received? TakeLatest() => Interlocked.Exchange(ref latest, null);

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                byte[] bytes;
                try
                {
                    bytes = client.Receive(ref remote);
                }
                catch (SocketException)
                {
                    if (!running) return;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                var line = Encoding.UTF8.GetString(bytes);
                switch (PosePacketCodec.TryDecode(line, out var frame))
                {
                    case PosePacketCodec.Result.Malformed:
                        Interlocked.Increment(ref malformed);
                        continue;
                    case PosePacketCodec.Result.VersionMismatch:
                        Interlocked.Increment(ref versionMismatch);
                        continue;
                }
                if (frame == null) continue;

                // 역행하는 시퀀스는 버린다 (늦게 도착한 옛 패킷).
                if (frame.Seq <= lastSeq)
                {
                    Interlocked.Increment(ref dropped);
                    continue;
                }
                lastSeq = frame.Seq;
                Volatile.Write(ref latest, new Received(frame, now));
            }
        }

        public void Dispose()
        {
            running = false;
            client.Close();
            if (thread.IsAlive) thread.Join(200);
        }
    }
}
