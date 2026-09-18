"""MediaPipe PoseLandmarker 래핑. Tasks API만 쓴다(§0-4, §9.2).

mediapipe.solutions.* 는 존재하지 않는다.
"""

from __future__ import annotations

import urllib.request
from pathlib import Path
from types import TracebackType

import mediapipe as mp
import numpy as np
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

MODEL_URL = (
    "https://storage.googleapis.com/mediapipe-models/pose_landmarker/"
    "pose_landmarker_lite/float16/latest/pose_landmarker_lite.task"
)
DEFAULT_MODEL_PATH = Path(__file__).resolve().parents[3] / "models" / "pose_landmarker_lite.task"


def download_model(dest: Path = DEFAULT_MODEL_PATH) -> Path:
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.exists():
        return dest
    print(f"downloading {MODEL_URL} -> {dest}")
    urllib.request.urlretrieve(MODEL_URL, dest)
    return dest


class PoseDetector:
    """VIDEO 모드: 프레임마다 단조 증가하는 타임스탬프(ms)를 넘겨 동기 호출한다."""

    def __init__(self, model_path: Path = DEFAULT_MODEL_PATH) -> None:
        if not model_path.exists():
            raise FileNotFoundError(
                f"model not found: {model_path}\n  run: uv run pose download-model"
            )
        options = vision.PoseLandmarkerOptions(
            base_options=mp_python.BaseOptions(model_asset_path=str(model_path)),
            running_mode=vision.RunningMode.VIDEO,
            num_poses=1,
        )
        self._landmarker = vision.PoseLandmarker.create_from_options(options)
        self._last_ts_ms = -1

    def detect(self, frame_bgr: np.ndarray, timestamp_ms: int) -> list | None:
        """33개 NormalizedLandmark 리스트, 못 찾으면 None.

        타임스탬프가 역행하면 1ms 앞으로 밀어 넣는다.
        """
        if timestamp_ms <= self._last_ts_ms:
            timestamp_ms = self._last_ts_ms + 1
        self._last_ts_ms = timestamp_ms

        rgb = frame_bgr[:, :, ::-1].copy()  # BGR → RGB; MediaPipe는 연속 메모리를 요구한다
        image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        result = self._landmarker.detect_for_video(image, timestamp_ms)
        if not result.pose_landmarks:
            return None
        return result.pose_landmarks[0]

    def close(self) -> None:
        self._landmarker.close()

    def __enter__(self) -> PoseDetector:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self.close()
