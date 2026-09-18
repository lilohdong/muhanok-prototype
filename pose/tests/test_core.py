from dataclasses import dataclass

import pytest

from pose.core import landmarks as L
from pose.core.packet import MAX_PACKET_BYTES, PROTOCOL_VERSION, build_packet, decode, encode


@dataclass
class FakeLandmark:
    x: float
    y: float
    visibility: float


def _all33(base_vis: float = 0.9) -> list[FakeLandmark]:
    return [FakeLandmark(i / 33, (i % 7) / 7, base_vis) for i in range(33)]


def test_extract_sends_only_13_indices() -> None:
    lm = L.extract(_all33())
    assert set(lm.keys()) == {str(i) for i in L.SENT_INDICES}
    assert len(lm) == 13
    assert lm["0"] == [0.0, 0.0, 0.9]


def test_extract_rounds_and_tolerates_short_lists() -> None:
    lm = L.extract([FakeLandmark(0.123456, 0.987654, 0.55555)])
    assert lm == {"0": [0.1235, 0.9877, 0.556]}


def test_shoulder_width() -> None:
    lm = {str(L.LEFT_SHOULDER): [0.3, 0.4, 0.9], str(L.RIGHT_SHOULDER): [0.7, 0.4, 0.9]}
    assert L.shoulder_width(lm) == pytest.approx(0.4)
    assert L.shoulder_width({}) is None


def test_upper_body_visible() -> None:
    lm = L.extract(_all33(0.9))
    assert L.upper_body_visible(lm, 0.5)
    lm[str(L.NOSE)][2] = 0.1
    assert not L.upper_body_visible(lm, 0.5)


def test_packet_roundtrip_and_size() -> None:
    lm = L.extract(_all33())
    packet = build_packet(seq=42, t=1758182400.123456, lm=lm)
    data = encode(packet)
    assert len(data) <= MAX_PACKET_BYTES
    assert b" " not in data
    back = decode(data)
    assert back["v"] == PROTOCOL_VERSION
    assert back["seq"] == 42
    assert back["t"] == 1758182400.123
    assert back["ok"] is True
    assert back["lm"]["11"] == lm["11"]


def test_lost_packet_has_no_lm() -> None:
    packet = build_packet(seq=1, t=1.0, lm=None)
    assert packet["ok"] is False
    assert "lm" not in packet
    assert decode(encode(packet))["ok"] is False


def test_decode_rejects_other_version() -> None:
    with pytest.raises(ValueError):
        decode('{"v":2,"seq":1,"t":0,"ok":false}')
