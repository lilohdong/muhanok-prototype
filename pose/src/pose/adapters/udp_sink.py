"""UDP 송신. TCP를 쓰지 않는다 — 포즈는 최신값만 의미 있고 재전송은 지연만 쌓는다(§7)."""

from __future__ import annotations

import socket
from types import TracebackType

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 52100


class UdpSink:
    def __init__(self, host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> None:
        self._addr = (host, port)
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._sock.setblocking(False)

    def send(self, data: bytes) -> None:
        try:
            self._sock.sendto(data, self._addr)
        except (BlockingIOError, InterruptedError, ConnectionResetError, OSError):
            # 수신자가 없으면 Windows는 ICMP 거부를 ConnectionResetError로 돌려준다. 다음 프레임에 다시 보낸다.
            pass

    def close(self) -> None:
        self._sock.close()

    def __enter__(self) -> UdpSink:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self.close()
