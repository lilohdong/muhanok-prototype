"""랜드마크 인덱스 상수와 순수 계산. IO·mediapipe·cv2 import 금지.

좌표 규약: MediaPipe 정규화 좌표(0~1). y는 아래로 갈수록 커진다. 여기서도 Unity에서도 부호를 뒤집지 않는다.
"""

from __future__ import annotations

from collections.abc import Sequence
from typing import Protocol

NOSE = 0
LEFT_SHOULDER = 11
RIGHT_SHOULDER = 12
LEFT_ELBOW = 13
RIGHT_ELBOW = 14
LEFT_WRIST = 15
RIGHT_WRIST = 16
LEFT_HIP = 23
RIGHT_HIP = 24
LEFT_KNEE = 25
RIGHT_KNEE = 26
LEFT_ANKLE = 27
RIGHT_ANKLE = 28

# §7: 33개 전부 보내지 않는다. 이 13개만.
SENT_INDICES: tuple[int, ...] = (
    NOSE,
    LEFT_SHOULDER,
    RIGHT_SHOULDER,
    LEFT_ELBOW,
    RIGHT_ELBOW,
    LEFT_WRIST,
    RIGHT_WRIST,
    LEFT_HIP,
    RIGHT_HIP,
    LEFT_KNEE,
    RIGHT_KNEE,
    LEFT_ANKLE,
    RIGHT_ANKLE,
)

# 프리뷰 오버레이용 뼈대 연결
BONES: tuple[tuple[int, int], ...] = (
    (LEFT_SHOULDER, RIGHT_SHOULDER),
    (LEFT_SHOULDER, LEFT_ELBOW),
    (LEFT_ELBOW, LEFT_WRIST),
    (RIGHT_SHOULDER, RIGHT_ELBOW),
    (RIGHT_ELBOW, RIGHT_WRIST),
    (LEFT_SHOULDER, LEFT_HIP),
    (RIGHT_SHOULDER, RIGHT_HIP),
    (LEFT_HIP, RIGHT_HIP),
    (LEFT_HIP, LEFT_KNEE),
    (LEFT_KNEE, LEFT_ANKLE),
    (RIGHT_HIP, RIGHT_KNEE),
    (RIGHT_KNEE, RIGHT_ANKLE),
)


class LandmarkLike(Protocol):
    """MediaPipe NormalizedLandmark와 호환되는 최소 인터페이스."""

    x: float
    y: float
    visibility: float


LandmarkMap = dict[str, list[float]]


def extract(landmarks: Sequence[LandmarkLike], precision: int = 4) -> LandmarkMap:
    """33개 랜드마크 중 SENT_INDICES만 골라 패킷용 dict로 만든다. 키는 문자열 인덱스, 값은 [x, y, visibility]."""
    out: LandmarkMap = {}
    for idx in SENT_INDICES:
        if idx >= len(landmarks):
            continue
        lm = landmarks[idx]
        out[str(idx)] = [
            round(float(lm.x), precision),
            round(float(lm.y), precision),
            round(float(lm.visibility), 3),
        ]
    return out


def shoulder_width(lm: LandmarkMap) -> float | None:
    """상반신 모드의 기준 단위(§8.6). 어깨가 둘 다 없으면 None."""
    left = lm.get(str(LEFT_SHOULDER))
    right = lm.get(str(RIGHT_SHOULDER))
    if left is None or right is None:
        return None
    return abs(left[0] - right[0])


def upper_body_visible(lm: LandmarkMap, min_visibility: float) -> bool:
    """캘리브레이션 안내용: 코와 양 어깨가 충분히 보이는가."""
    for idx in (NOSE, LEFT_SHOULDER, RIGHT_SHOULDER):
        v = lm.get(str(idx))
        if v is None or v[2] < min_visibility:
            return False
    return True
