"""OpenCV 카메라. 640×480 @ 30fps 고정(§9.2). Windows에서는 CAP_DSHOW로 초기화가 빠르다."""

from __future__ import annotations

import sys
from types import TracebackType

import cv2
import numpy as np

FRAME_WIDTH = 640
FRAME_HEIGHT = 480
FRAME_FPS = 30


class Camera:
    def __init__(self, index: int = 0) -> None:
        backend = cv2.CAP_DSHOW if sys.platform == "win32" else cv2.CAP_ANY
        self._cap = cv2.VideoCapture(index, backend)
        if not self._cap.isOpened():
            raise RuntimeError(f"camera {index} could not be opened")
        self._cap.set(cv2.CAP_PROP_FRAME_WIDTH, FRAME_WIDTH)
        self._cap.set(cv2.CAP_PROP_FRAME_HEIGHT, FRAME_HEIGHT)
        self._cap.set(cv2.CAP_PROP_FPS, FRAME_FPS)
        # 드라이버 버퍼에 프레임이 쌓이면 지연이 누적된다. 최신 프레임만.
        self._cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

    def read(self) -> np.ndarray | None:
        ok, frame = self._cap.read()
        return frame if ok else None

    def close(self) -> None:
        self._cap.release()

    def __enter__(self) -> Camera:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self.close()
