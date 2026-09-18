"""디버그 프리뷰 창. 랜드마크·뼈대·상반신 캘리브레이션 안내를 그린다."""

from __future__ import annotations

import cv2
import numpy as np

from pose.core.landmarks import BONES, LandmarkMap, upper_body_visible

WINDOW = "muhanok pose (q: quit)"
MIN_VISIBILITY = 0.5


class Preview:
    def __init__(self) -> None:
        cv2.namedWindow(WINDOW, cv2.WINDOW_AUTOSIZE)

    def show(self, frame_bgr: np.ndarray, lm: LandmarkMap | None, status: str) -> bool:
        """그리고 표시한다. 사용자가 q를 누르면 False."""
        h, w = frame_bgr.shape[:2]
        canvas = frame_bgr

        if lm is not None:
            for a, b in BONES:
                pa, pb = lm.get(str(a)), lm.get(str(b))
                if pa is None or pb is None:
                    continue
                if pa[2] < MIN_VISIBILITY or pb[2] < MIN_VISIBILITY:
                    continue
                cv2.line(canvas, _px(pa, w, h), _px(pb, w, h), (0, 200, 255), 2)
            for _key, p in lm.items():
                color = (0, 255, 0) if p[2] >= MIN_VISIBILITY else (0, 0, 255)
                cv2.circle(canvas, _px(p, w, h), 4, color, -1)

        guide = (
            "OK: head + shoulders in frame"
            if lm is not None and upper_body_visible(lm, MIN_VISIBILITY)
            else "Step back: show head and both shoulders"
        )
        cv2.putText(canvas, status, (8, 20), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1)
        cv2.putText(canvas, guide, (8, h - 12), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 255, 255), 1)

        cv2.imshow(WINDOW, canvas)
        return (cv2.waitKey(1) & 0xFF) != ord("q")

    def close(self) -> None:
        cv2.destroyWindow(WINDOW)


def _px(p: list[float], w: int, h: int) -> tuple[int, int]:
    return int(p[0] * w), int(p[1] * h)
