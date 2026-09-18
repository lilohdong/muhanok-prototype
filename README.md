# 무한옥 프로토타입

카메라로 몸동작을 인식해 조작하는 PC용 3레인 엔드리스 러너 프로토타입. 사양은 [CLAUDE.md](CLAUDE.md).

이 환경은 **노트북 내장 캠(머리~어깨)** 기준이라 상반신 모드가 기본이다:
Jump = 몸 전체가 뜸, Duck = 머리가 내려감, 계단(KneeRaise) = **한 손 들기**, 사이드 스텝 = 몸을 좌우로 옮김.

## 빠른 시작

### 1. Unity (키보드 모드 — Python 없이도 돌아감)

1. Unity Hub에서 `unity/muhanok-prototype/` 열기 (6000.3.24f1). 처음 열면 VContainer/UniTask를 git으로 받는다.
2. 메뉴 **Muhanok → Create Default Settings Assets** → `Assets/Settings/`에 `GameConfig`, `TuningProfile`, `PresentationProfile` 생성.
3. `Assets/Scenes/SampleScene` 에서 빈 GameObject 생성 → 이름 `GameLifetimeScope` → **Game Lifetime Scope** 컴포넌트 추가 → `Game Config` 슬롯에 `GameConfig.asset` 드래그.
4. Play. `Space`=Jump, `W`=KneeRaise, `S`=Duck, `A`/`D`=StepLeft/Right.

카메라·조명·플레이어·HUD·구간은 전부 런타임에 코드가 만든다. 씬에는 위 오브젝트 하나면 된다.

### 2. Python 포즈 송신기

```bash
cd pose
uv sync                       # Python 3.12 + mediapipe 0.10.21 (uv가 파이썬도 받아준다)
uv run pose download-model    # models/pose_landmarker_lite.task
uv run pose live --preview    # 카메라 → UDP 127.0.0.1:52100
```

프리뷰 창 하단에 "OK: head + shoulders in frame" 이 뜨는 위치에 선다. `q`로 종료.

### 3. 카메라로 플레이

`GameConfig.asset` → `Input Source` 를 **Pose** 로 바꾸고 Play. HUD에 지연(ms)과 포즈 상태, 우하단에 랜드마크 미리보기가 뜬다.
임계값은 `TuningProfile.asset`에서 **Play 중에** 바꿀 수 있다 (바꾸면 베이스라인이 1초간 다시 잡힌다).

### 4. 녹화 / 재생 (앉아서 튜닝)

```bash
uv run pose live --record ../recordings/jump_x10.ndjson   # 점프 10번 하고 Ctrl+C
uv run pose replay ../recordings/jump_x10.ndjson --loop   # 카메라 없이 재생 → Unity가 똑같이 반응
```

파일 이름을 `<동작>_x<횟수>.ndjson` (`jump`, `duck`, `armraise`, `stepleft`, `stepright`) 으로 두면
Unity **Test Runner → EditMode** 의 `RecordingRegressionTests` 가 자동으로 "정확히 N회 검출"을 검증한다.

## 테스트

- Unity: Window → General → Test Runner → EditMode → Run All (ClearanceRule 전수, 코덱, 판정기, 세션)
- Python: `cd pose && uv run pytest && uv run ruff check`

## 구조

```
unity/muhanok-prototype/Assets/Muhanok/
  Domain/         게임 규칙 (엔진 참조 없음)      ClearanceRule, RunState, RunRules
  Application/    유스케이스·포트·포즈 판정기     RunSession, Track, PostureDetector, PosePacketCodec
  Infrastructure/ UDP, 시계, 난수, TuningProfile   UdpPoseReceiver, PoseActionSource
  Presentation/   MonoBehaviour·뷰·HUD·키보드      WorldPresenter, KeyboardActionSource
  Composition/    VContainer 배선                  GameLifetimeScope, GameConfig
pose/src/pose/    core(순수) / adapters(cv2, mediapipe, udp, file) / main.py
protocol/         pose_packet.schema.json
recordings/       *.ndjson
```
