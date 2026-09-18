"""CLI 조립(§9.3).

uv run pose live [--record PATH] [--preview] [--camera N] [--host H] [--port P]
uv run pose replay PATH [--loop] [--speed X] [--host H] [--port P]
uv run pose download-model
"""

from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path

from pose.adapters.udp_sink import DEFAULT_HOST, DEFAULT_PORT, UdpSink
from pose.core.packet import build_packet, encode


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="pose", description="Muhanok pose sender")
    sub = parser.add_subparsers(dest="command", required=True)

    live = sub.add_parser("live", help="camera -> MediaPipe -> UDP")
    live.add_argument("--record", type=Path, help="also write every packet to this .ndjson")
    live.add_argument("--preview", action="store_true", help="show landmark overlay window")
    live.add_argument("--camera", type=int, default=0)
    live.add_argument("--model", type=Path, default=None)
    _net_args(live)

    replay = sub.add_parser("replay", help="replay a recording to UDP with original timing")
    replay.add_argument("path", type=Path)
    replay.add_argument("--loop", action="store_true")
    replay.add_argument("--speed", type=float, default=1.0, help="1.0 = original timing")
    _net_args(replay)

    sub.add_parser("download-model", help="fetch pose_landmarker_lite.task into models/")

    args = parser.parse_args(argv)
    if args.command == "live":
        return run_live(args)
    if args.command == "replay":
        return run_replay(args)
    if args.command == "download-model":
        from pose.adapters.detector import download_model

        print(download_model())
        return 0
    return 2


def _net_args(p: argparse.ArgumentParser) -> None:
    p.add_argument("--host", default=DEFAULT_HOST)
    p.add_argument("--port", type=int, default=DEFAULT_PORT)


def run_live(args: argparse.Namespace) -> int:
    # 무거운 import는 여기서. replay는 카메라도 mediapipe도 필요 없다.
    from pose.adapters.camera import Camera
    from pose.adapters.detector import DEFAULT_MODEL_PATH, PoseDetector
    from pose.adapters.file_sink import FileSink
    from pose.core.landmarks import extract

    model = args.model or DEFAULT_MODEL_PATH
    recorder = FileSink(args.record) if args.record else None
    preview = None
    if args.preview:
        from pose.adapters.preview import Preview

        preview = Preview()

    seq = 0
    sent = 0
    detected = 0
    t_start = time.time()
    fps_window_start = t_start
    fps_window_frames = 0
    fps = 0.0

    try:
        with (
            Camera(args.camera) as cam,
            PoseDetector(model) as det,
            UdpSink(args.host, args.port) as udp,
        ):
            print(
                f"live: camera={args.camera} -> udp://{args.host}:{args.port}"
                + (f"  record={args.record}" if recorder else "")
            )
            while True:
                frame = cam.read()
                if frame is None:
                    print("camera read failed", file=sys.stderr)
                    return 1

                now = time.time()
                landmarks = det.detect(frame, int((now - t_start) * 1000))
                lm = extract(landmarks) if landmarks is not None else None
                packet = build_packet(seq, now, lm)
                data = encode(packet)
                udp.send(data)
                if recorder:
                    recorder.write(data)

                seq += 1
                sent += 1
                if lm is not None:
                    detected += 1
                fps_window_frames += 1
                if now - fps_window_start >= 1.0:
                    fps = fps_window_frames / (now - fps_window_start)
                    fps_window_start = now
                    fps_window_frames = 0
                    if not preview:
                        line = f"\rseq={seq} fps={fps:.1f} detected={detected}/{sent}"
                        print(line, end="", flush=True)

                if preview is not None:
                    status = f"seq={seq} fps={fps:.1f} ok={lm is not None} {len(data)}B"
                    if not preview.show(frame, lm, status):
                        break
    except KeyboardInterrupt:
        pass
    finally:
        if recorder:
            recorder.close()
            print(f"\nrecorded {recorder.count} frames -> {args.record}")
        if preview is not None:
            preview.close()
    print()
    return 0


def run_replay(args: argparse.Namespace) -> int:
    """원래 프레임 간격대로 재생한다.

    seq는 이어서 매기고 t는 지금 시각으로 바꾼다.
    Unity 쪽 역행 필터와 지연 측정이 그대로 동작하게 하기 위해서다.
    """
    from pose.adapters.file_sink import read_ndjson

    packets = list(read_ndjson(args.path))
    if not packets:
        print(f"empty recording: {args.path}", file=sys.stderr)
        return 1
    speed = max(args.speed, 1e-3)
    seq = 0
    passes = 0

    try:
        with UdpSink(args.host, args.port) as udp:
            print(
                f"replay: {args.path} ({len(packets)} frames) -> udp://{args.host}:{args.port}"
                f"  loop={args.loop} speed={speed}"
            )
            while True:
                base_t = packets[0]["t"]
                wall_start = time.perf_counter()
                for p in packets:
                    due = wall_start + (p["t"] - base_t) / speed
                    delay = due - time.perf_counter()
                    if delay > 0:
                        time.sleep(delay)
                    out = dict(p)
                    out["seq"] = seq
                    out["t"] = round(time.time(), 3)
                    udp.send(encode(out))
                    seq += 1
                passes += 1
                print(f"\rpass {passes} seq={seq}", end="", flush=True)
                if not args.loop:
                    break
    except KeyboardInterrupt:
        pass
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
