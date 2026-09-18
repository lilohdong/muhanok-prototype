"""§7 패킷 직렬화. IO 없음. 스키마 단일 원본은 protocol/pose_packet.schema.json."""

from __future__ import annotations

import json
from typing import Any

from pose.core.landmarks import LandmarkMap

PROTOCOL_VERSION = 1
MAX_PACKET_BYTES = 1024

Packet = dict[str, Any]


def build_packet(seq: int, t: float, lm: LandmarkMap | None) -> Packet:
    """한 프레임 → 패킷 dict. lm이 None이면 인식 실패(ok=false)이고 lm 키를 넣지 않는다."""
    packet: Packet = {"v": PROTOCOL_VERSION, "seq": seq, "t": round(t, 3), "ok": lm is not None}
    if lm is not None:
        packet["lm"] = lm
    return packet


def encode(packet: Packet) -> bytes:
    """한 줄 UTF-8 JSON. 공백 없음. 1KB를 넘으면 프로토콜 위반이므로 예외."""
    data = json.dumps(packet, separators=(",", ":"), ensure_ascii=True).encode("utf-8")
    if len(data) > MAX_PACKET_BYTES:
        raise ValueError(f"packet too large: {len(data)} bytes > {MAX_PACKET_BYTES}")
    return data


def decode(line: str | bytes) -> Packet:
    """녹화 파일의 한 줄 → 패킷 dict. 버전이 다르면 예외."""
    packet = json.loads(line)
    if not isinstance(packet, dict) or packet.get("v") != PROTOCOL_VERSION:
        raise ValueError(f"unsupported packet: {line!r}")
    return packet
