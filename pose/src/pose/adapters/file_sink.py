"""NDJSON 녹화/읽기. 한 줄 = UDP로 나간 패킷 그대로. Unity 회귀 테스트가 이 파일을 그대로 먹는다(§12-2)."""

from __future__ import annotations

from collections.abc import Iterator
from pathlib import Path
from types import TracebackType

from pose.core.packet import Packet, decode


class FileSink:
    def __init__(self, path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        self._f = path.open("wb")
        self.count = 0

    def write(self, data: bytes) -> None:
        self._f.write(data)
        self._f.write(b"\n")
        self.count += 1

    def close(self) -> None:
        self._f.flush()
        self._f.close()

    def __enter__(self) -> FileSink:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self.close()


def read_ndjson(path: Path) -> Iterator[Packet]:
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                yield decode(line)
