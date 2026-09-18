# 무한옥 (Muhanok) — 프로토타입 개발 사양서

> 이 문서는 AI 코딩 에이전트(Claude Code 등)가 이 저장소에서 작업할 때 따르는 **단일 기준 문서**다.
> 사양과 실제 코드가 어긋나면 이 문서가 우선한다. 사양을 바꿔야 한다면 **코드보다 이 문서를 먼저 고친다.**

---

## 0. 에이전트 작업 규칙

작업을 시작하기 전에 반드시 읽고 지킨다.

1. **§4 의존성 규칙을 위반하는 코드는 절대 작성하지 않는다.** 위반이 불가피해 보이면 코드를 쓰지 말고 먼저 질문한다.
2. **새 외부 패키지를 추가하지 않는다.** §2에 없는 의존성이 필요하면 먼저 제안하고 승인을 받는다.
3. **Unity 씬(.unity) 파일은 직접 편집하지 않는다.** 씬 배치가 필요하면 "씬에서 이렇게 해주세요"를 단계별로 안내한다. 코드로 만들 수 있는 것은 프리팹/`ScriptableObject`/런타임 생성으로 처리한다.
4. **MediaPipe는 Tasks API만 쓴다.** `mediapipe.solutions.*` 는 최신 버전에서 제거되었다. `mediapipe.tasks.python.vision.PoseLandmarker` + `.task` 모델 파일 로드 방식만 사용한다. 구버전 API가 든 코드를 생성했다면 스스로 폐기하고 다시 쓴다.
5. **마일스톤(§10)을 건너뛰지 않는다.** 한 번에 하나씩 끝내고, 각 마일스톤의 완료 조건(DoD)을 만족시킨 뒤 다음으로 간다.
6. **매직 넘버 금지.** 판정 임계값·속도·거리 등 튜닝 값은 전부 `ScriptableObject` 또는 `TuningProfile`에 모은다. 코드에 숫자를 박지 않는다.
7. 추측하지 말고 물어본다. 사양에 없는 결정(예: 카메라 각도, 점수 공식)이 필요하면 임의로 정하지 말고 질문한다.

---

## 1. 프로젝트 개요

카메라로 사용자의 전신 동작을 인식해서 조작하는 **PC용 3레인 엔드리스 러너**의 프로토타입.

주인공은 감옥을 탈출하기 위해 자동으로 앞으로 달리고, 뒤에서 교도관이 쫓아온다. 플레이어는 키보드가 아니라 **실제 몸동작**으로 장애물을 피한다.

| 장애물 | 요구 동작 |
|---|---|
| 계단 (Stairs) | 무릎 들기 (KneeRaise) |
| 바리케이드 (Barricade) | 점프 (Jump) |
| 철창 (Cage) | 엎드리기 (Duck) |
| 경찰차 (PoliceCar) | 사이드 스텝 (StepLeft / StepRight) |

### 카메라 환경 제약 (2026-09-18 확정)

개발 환경에는 각도가 높은 웹캠이 없고 **노트북 내장 캠만** 있다. 카메라는 **머리 ~ 어깨선 살짝 아래**까지만 찍는다.
따라서 골반(23/24)·무릎(25/26)·발목(27/28)은 **화면 밖**이며 신뢰할 수 없다.

- 판정기는 **`BodyMode.UpperBody`** 를 기본으로 하고, 전신 캠이 생기면 `BodyMode.FullBody`(§8.3 원안)로 전환할 수 있게 둘 다 구현한다.
- 상반신 모드에서 계단(Stairs)의 요구 동작은 **무릎 들기 대신 "한 손 들기(ArmRaise)"** 로 감지한다. 게임 도메인의 이름(`PlayerAction.KneeRaise`, `PostureState.KneeRaised`)은 그대로 두고, **어떤 제스처가 그 액션을 만드는지는 판정기(Infrastructure)만 안다.** 전신 캠으로 바꾸면 판정기만 바뀌고 게임은 그대로다.
- 상반신 모드의 구체적 규칙은 §8.6.

### 프로토타입 범위

**포함한다**
- 3레인 이동, 4종 동작 인식, 4종 장애물, 무한 맵 생성
- 충돌 → 넘어짐 → 교도관 추격 거리 감소 → 잡히면 종료
- 코인 획득, 거리 기반 점수, 최소 HUD
- 키보드 입력 모드(디버그) — 포즈 입력과 100% 호환
- 포즈 데이터 녹화/재생
- 종단 지연(latency) 측정 및 표시

**포함하지 않는다** (프로토타입 이후)
- 상점, 아이템, 세이브 데이터, 사운드
- 캐릭터 모델링·애니메이션 — **모든 오브젝트는 프리미티브(Capsule/Cube/Cylinder)와 단색 머티리얼로 대체한다**
- 메뉴, 설정 화면, 로컬라이제이션
- 빌드 배포(exe 패키징)

---

## 2. 기술 스택 (버전 고정)

### Unity 측
| 항목 | 값 | 비고 |
|---|---|---|
| Unity | **6.3 LTS (6000.3.x)** | 팀 전체 패치 버전까지 동일하게 |
| 렌더 파이프라인 | **Built-in** | 프로젝트가 3D(Core) 템플릿으로 생성됨. 프리미티브+단색만 쓰므로 URP 전환 불필요 |
| DI | **VContainer** (`jp.hadashikick.vcontainer`) | Git URL (`manifest.json`에 태그 고정) |
| 비동기 | **UniTask** (`com.cysharp.unitask`) | 코루틴 대신 사용 |
| 입력 | **Input System** (`com.unity.inputsystem`) | 구 Input Manager 사용 금지 |
| 풀링 | `UnityEngine.Pool.ObjectPool<T>` | **내장 기능. 직접 구현하지 말 것** |
| 테스트 | Unity Test Framework (NUnit) | EditMode 전용 |
| 직렬화 | `System.Text.Json` 또는 `Unity.Serialization` | Newtonsoft 불필요 |

> C# 언어 버전은 Unity 6 기준 **C# 9**이다. `record`, nullable reference types는 쓸 수 있다.
> `file-scoped namespace`(C# 10)는 컴파일 에러가 나면 블록형으로 되돌린다.

### Python 측
| 항목 | 값 |
|---|---|
| Python | **3.12** (3.13 금지 — MediaPipe 휠 없음) |
| 패키지 관리 | **uv** (`uv sync` / `uv run`) |
| 핵심 의존성 | `mediapipe`, `opencv-contrib-python`(mediapipe가 contrib를 요구. `opencv-python`과 같이 깔면 `cv2` 충돌), `numpy<2` |
| 린트/포맷 | `ruff` |
| 테스트 | `pytest` |

`pyproject.toml`에 버전을 **정확히 고정**한다 (`mediapipe==0.10.x`). MediaPipe는 마이너 버전마다 API를 깬 전적이 있다.

---

## 3. 저장소 구조

```
muhanok/
├─ CLAUDE.md                       # 이 문서
├─ .gitignore                      # GitHub 공식 Unity 템플릿
├─ .gitattributes                  # Git LFS 추적 설정
│
├─ unity/muhanok-prototype/        # Unity 프로젝트 루트 (Hub가 만든 하위 폴더 그대로 사용)
│  └─ Assets/
│     ├─ Muhanok/
│     │  ├─ Domain/                # 순수 C#, 엔진 참조 없음
│     │  │  └─ Muhanok.Domain.asmdef
│     │  ├─ Application/           # 순수 C#, 엔진 참조 없음
│     │  │  └─ Muhanok.Application.asmdef
│     │  ├─ Infrastructure/        # 엔진 참조 O, 씬 의존 X
│     │  │  └─ Muhanok.Infrastructure.asmdef
│     │  ├─ Presentation/          # MonoBehaviour, View, Prefab
│     │  │  └─ Muhanok.Presentation.asmdef
│     │  └─ Composition/           # LifetimeScope (합성 루트)
│     │     └─ Muhanok.Composition.asmdef
│     ├─ Settings/                 # ScriptableObject 에셋
│     └─ Tests/
│        └─ EditMode/
│           └─ Muhanok.Tests.EditMode.asmdef
│
├─ pose/                           # Python 포즈 인식 앱
│  ├─ pyproject.toml
│  ├─ src/pose/
│  │  ├─ core/                     # 순수 함수 (정규화, 판정 보조). IO 없음
│  │  ├─ adapters/                 # camera.py, udp_sink.py, file_sink.py
│  │  └─ main.py
│  ├─ models/                      # pose_landmarker_lite.task (LFS 또는 gitignore)
│  └─ tests/
│
├─ protocol/
│  └─ pose_packet.schema.json      # §7 프로토콜 단일 원본
│
└─ recordings/                     # 녹화된 포즈 로그 (.ndjson)
```

---

## 4. 아키텍처 — 클린 아키텍처

### 계층과 의존 방향

```
        Presentation ──┐
                       ├──▶ Application ──▶ Domain
      Infrastructure ──┘
                 ▲
          Composition (모든 계층을 알고, 아무도 모름)
```

**의존성 규칙: 화살표는 항상 안쪽(Domain)을 향한다. 역방향 참조는 금지.**

| 계층 | 책임 | 할 수 있는 것 | 절대 금지 |
|---|---|---|---|
| **Domain** | 게임 규칙 자체 | 값 객체, 엔티티, 순수 함수 | `using UnityEngine`, IO, 시간, 랜덤, 로그 |
| **Application** | 유스케이스 조립 | Domain 호출, 포트 인터페이스 정의 | `using UnityEngine`, 구체 구현 참조 |
| **Infrastructure** | 포트의 실제 구현 | UDP, 파일, 시간, 난수, 카메라 | Presentation 참조 |
| **Presentation** | 보여주기와 사용자 입력 | MonoBehaviour, Prefab, Animator | 게임 규칙 판단, Application 우회 |
| **Composition** | 의존성 배선 | VContainer 등록 | 로직 |

### 강제 방법 — asmdef

**말로만 지키지 말고 컴파일러가 막게 한다.**

`Muhanok.Domain.asmdef`:
```json
{
  "name": "Muhanok.Domain",
  "references": [],
  "noEngineReferences": true,
  "autoReferenced": false
}
```

`Muhanok.Application.asmdef`:
```json
{
  "name": "Muhanok.Application",
  "references": ["Muhanok.Domain"],
  "noEngineReferences": true,
  "autoReferenced": false
}
```

`noEngineReferences: true` 가 핵심이다. 이게 켜져 있으면 Domain/Application에서 `using UnityEngine;` 을 쓰는 순간 **빌드가 깨진다.** 규율이 아니라 구조로 막는다.

Infrastructure / Presentation / Composition은 `noEngineReferences`를 빼고, `references`에 자기보다 안쪽 어셈블리만 넣는다.

### 이 구조가 이 프로젝트에서 갖는 실질적 의미

동작 인식은 불안정하고 계속 바뀐다. 게임 규칙은 안 바뀐다.
**둘을 어셈블리 단위로 분리해 두면, 인식 로직을 몇 번을 갈아엎어도 게임 규칙 코드와 테스트는 건드릴 일이 없다.**
그리고 Domain/Application이 엔진을 모르기 때문에 그 계층의 테스트는 Unity를 띄우지 않고 밀리초 단위로 돌아간다.

---

## 5. 도메인 모델

`Muhanok.Domain` 에 정의한다. 전부 불변(immutable).

```csharp
namespace Muhanok.Domain
{
    public enum Lane { Left = -1, Center = 0, Right = 1 }

    public enum PlayerAction { Jump, KneeRaise, Duck, StepLeft, StepRight }

    public enum ObstacleKind { Stairs, Barricade, Cage, PoliceCar }

    /// 플레이어의 세로 자세 상태.
    public enum PostureState { Grounded, Airborne, KneeRaised, Ducking }

    public readonly struct ObstaclePlacement
    {
        public readonly ObstacleKind Kind;
        public readonly Lane Lane;
        public readonly float DistanceFromSegmentStart;
    }

    public readonly struct CoinPlacement
    {
        public readonly Lane Lane;
        public readonly float DistanceFromSegmentStart;
    }

    /// 한 구간의 배치도. 프리팹이 아니라 '데이터'다.
    public sealed class SegmentLayout
    {
        public float Length { get; }
        public IReadOnlyList<ObstaclePlacement> Obstacles { get; }
        public IReadOnlyList<CoinPlacement> Coins { get; }
    }

    /// 한 판의 진행 상태.
    public sealed class RunState
    {
        public float DistanceTravelled { get; }
        public float ForwardSpeed { get; }
        public Lane CurrentLane { get; }
        public PostureState Posture { get; }
        public float ChaserGap { get; }   // 교도관과의 거리. 0 이하면 종료
        public int Coins { get; }
        public int Score { get; }
        public bool IsOver { get; }
    }
}
```

### 핵심 규칙 — 순수 함수로 분리한다

이 프로젝트에서 **가장 중요한 도메인 규칙**은 "이 자세로 이 장애물을 통과할 수 있는가"다.
반드시 아래 형태의 정적 순수 함수로 만든다. 테스트하기 쉽고, 나중에 장애물을 추가해도 한 곳만 고치면 된다.

```csharp
public static class ClearanceRule
{
    /// 부딪히지 않고 통과하면 true.
    public static bool Clears(
        ObstacleKind kind,
        Lane obstacleLane,
        Lane playerLane,
        PostureState posture);
}
```

규칙 표:

| 장애물 | 통과 조건 |
|---|---|
| Stairs | 같은 레인일 때 `Posture == KneeRaised` 또는 `Airborne` |
| Barricade | 같은 레인일 때 `Posture == Airborne` |
| Cage | 같은 레인일 때 `Posture == Ducking` |
| PoliceCar | **레인이 다르기만 하면 통과.** 자세는 무관 |

다른 레인이면 언제나 통과한다. 이 표가 곧 유닛 테스트 케이스다 (§12).

---

## 6. 포트 (Application이 정의하는 인터페이스)

Application은 이 인터페이스들만 알고, 구현은 Infrastructure/Presentation이 제공한다.

```csharp
namespace Muhanok.Application.Ports
{
    /// 플레이어 동작의 출처. 키보드든 카메라든 여기 뒤에 숨는다.
    public interface IPlayerActionSource
    {
        event Action<PlayerAction> ActionDetected;
    }

    /// 구간 배치도 생성기. 시드를 받아 결정적으로 동작해야 한다.
    public interface ISegmentLayoutGenerator
    {
        SegmentLayout Next(int segmentIndex);
    }

    /// 게임 상태를 화면에 반영하는 출력 포트.
    public interface IRunPresenter
    {
        void OnStateChanged(RunState state);
        void OnPlayerStumbled();
        void OnCoinCollected(int total);
        void OnRunEnded(int finalScore);
    }

    public interface IClock { float DeltaTime { get; } double NowSeconds { get; } }

    /// 지연 측정 등 계측값 수집.
    public interface ITelemetrySink { void RecordLatency(double milliseconds); }
}
```

### `IPlayerActionSource`가 이 설계의 심장이다

구현체는 두 개다.
- `KeyboardActionSource` (Presentation) — Space/W/S/A/D
- `PoseActionSource` (Infrastructure) — UDP 수신 + 동작 판정

**게임 로직은 어느 쪽이 꽂혔는지 전혀 모른다.** 덕분에
- 카메라 없이 게임 전체를 개발하고 테스트할 수 있고,
- 버그가 났을 때 "게임 버그인가 인식 버그인가"를 소스만 바꿔서 즉시 판별할 수 있다.

**이것을 먼저 만들고, 그 다음에 나머지를 만든다.**

---

## 7. 통신 프로토콜 (Python → Unity)

### 전송
- **UDP, `127.0.0.1:52100`**
- TCP를 쓰지 않는다. 포즈 데이터는 최신값만 의미가 있고, TCP는 재전송하느라 밀린 프레임을 쌓아 지연을 누적시킨다.
- 송신 주기: 카메라 프레임마다 (30fps 목표)
- 한 패킷 = 한 프레임. 1KB를 넘지 않게 유지한다.

### 패킷 포맷 (UTF-8 JSON, 한 줄)

```json
{
  "v": 1,
  "seq": 1042,
  "t": 1758182400.123,
  "ok": true,
  "lm": {
    "0":  [0.501, 0.180, 0.92],
    "11": [0.430, 0.310, 0.95],
    "12": [0.572, 0.309, 0.95],
    "23": [0.448, 0.560, 0.93],
    "24": [0.556, 0.561, 0.93],
    "25": [0.445, 0.740, 0.88],
    "26": [0.559, 0.742, 0.88],
    "27": [0.442, 0.910, 0.81],
    "28": [0.561, 0.912, 0.81]
  }
}
```

| 필드 | 의미 |
|---|---|
| `v` | 프로토콜 버전. 바뀌면 Unity는 경고 후 패킷 폐기 |
| `seq` | 단조 증가 시퀀스. 역행하는 패킷은 버린다 |
| `t` | Python 측 송신 시각 (Unix epoch, 초, 소수점 포함) |
| `ok` | 전신 인식 성공 여부. `false`면 `lm` 없음 |
| `lm` | 랜드마크. 키는 MediaPipe 인덱스, 값은 `[x, y, visibility]` (정규화 좌표 0~1) |

**33개 전부 보내지 않는다.** 아래 13개만 보낸다. (상반신 모드용 팔꿈치/손목 4개가 원안 9개에 추가됨. 1KB 제한은 여전히 지킨다.)

| 인덱스 | 부위 | 상반신 모드 |
|---|---|---|
| 0 | 코 (nose) | 사용 |
| 11 / 12 | 왼쪽 / 오른쪽 어깨 | 사용 (기준 단위) |
| 13 / 14 | 왼쪽 / 오른쪽 팔꿈치 | 사용 |
| 15 / 16 | 왼쪽 / 오른쪽 손목 | 사용 (ArmRaise) |
| 23 / 24 | 왼쪽 / 오른쪽 골반 | 화면 밖 — 무시 |
| 25 / 26 | 왼쪽 / 오른쪽 무릎 | 화면 밖 — 무시 |
| 27 / 28 | 왼쪽 / 오른쪽 발목 | 화면 밖 — 무시 |

화면 밖 랜드마크도 MediaPipe가 추정값을 내놓지만 `visibility`가 낮다. Python은 그대로 보내고, Unity 판정기가 `visibility < minVisibility`면 그 랜드마크를 **없는 것으로** 취급한다.

> MediaPipe의 `y`는 **아래로 갈수록 커진다.** Unity로 넘긴 뒤 부호를 뒤집지 말고, 판정 코드 안에서 일관되게 "y가 작아지면 위로 올라간 것"으로 다룬다. 이 규칙을 코드 주석에 반드시 명시한다.

### 지연 측정
Unity는 패킷 수신 시 `(Unity의 현재 Unix 시각 - t)` 를 계산해 `ITelemetrySink`로 보낸다.
클럭 차이 때문에 절대값은 부정확할 수 있으므로, **같은 PC에서만 의미 있는 상대 지표**로 취급하고 HUD에 이동 평균을 표시한다.

---

## 8. 동작 판정 사양

**판정은 Python이 아니라 Unity(Infrastructure)에서 한다.** 좌표만 넘기고 해석은 게임 쪽에서 해야 인게임 상태를 같이 볼 수 있고, 인스펙터에서 실시간 튜닝이 가능하다.

### 8.1 정규화 (필수)

픽셀/정규화 좌표를 그대로 쓰면 사용자가 카메라에 가까이 서기만 해도 판정이 전부 틀어진다. 매 프레임 아래를 계산한다.

```
hipMid      = (lm[23] + lm[24]) / 2
shoulderMid = (lm[11] + lm[12]) / 2
torso       = |shoulderMid.y - hipMid.y|      // 기준 단위(unit)
shoulderW   = |lm[11].x - lm[12].x|
```

이후 **모든 임계값은 `torso` 또는 `shoulderW`의 배수로만 표현한다.** 절대 좌표 임계값 금지.

### 8.2 베이스라인

서 있는 상태의 기준값이 필요하다.
- 최근 1초간, **어떤 동작도 감지되지 않은 프레임**들의 `hipMid.y`, `hipMid.x`, `noseY`의 **중앙값**을 베이스라인으로 유지한다.
- 평균이 아니라 중앙값을 쓴다. 튀는 프레임 하나에 기준이 흔들리지 않게 하기 위해서다.

### 8.3 판정 규칙 — 전신 모드 `BodyMode.FullBody` (v1 — 전부 `TuningProfile`에서 조정 가능하게)

| 동작 | 조건 | 초기 임계값 |
|---|---|---|
| **Jump** | `baselineHipY - hipMid.y > T_jump * torso` | `T_jump = 0.22` |
| **KneeRaise** | 한쪽 `(hipY - kneeY) / torso > T_knee` **이고** 반대쪽은 `< T_knee * 0.6` | `T_knee = 0.55` |
| **Duck** | `noseY - baselineNoseY > T_duck * torso` | `T_duck = 0.40` |
| **StepLeft/Right** | `\|hipMid.x - baselineHipX\| > T_step * shoulderW`, 부호로 방향 결정 | `T_step = 0.45` |

### 8.4 안정화 3종 세트 (전부 필수)

임계값 하나만 쓰면 경계에서 덜덜 떨리며 오발이 난다. 셋을 모두 적용한다.

1. **N프레임 확인** — 조건을 연속 `N = 2` 프레임 만족해야 인정
2. **히스테리시스** — 진입 임계값과 해제 임계값을 다르게 둔다. 해제는 진입의 `0.7`배
3. **쿨다운** — 동작 하나가 인정되면 `300ms` 동안 같은 동작을 다시 받지 않는다

### 8.5 상태 기계

`PostureDetector`는 프레임 입력을 받아 `PostureState`를 출력하는 **순수한 상태 기계**로 만든다.
Unity 타입에 의존시키지 말고, 입력을 `PoseFrame`(순수 구조체)으로 추상화해서 **Application 계층에 두고 유닛 테스트한다.** 녹화 파일(§9.3)을 그대로 먹여서 회귀 테스트가 가능해진다.

### 8.6 상반신 모드 (`BodyMode.UpperBody`) — 이 환경의 기본값

§1의 카메라 제약 때문에 골반·무릎·발목을 쓸 수 없다. §8.1~8.4의 **구조는 그대로** 두고 신호만 바꾼다.

**정규화 단위**
```
shoulderMid = (lm[11] + lm[12]) / 2
shoulderW   = |lm[11].x - lm[12].x|        // 기준 단위. 베이스라인의 중앙값(W)을 쓴다
noseY       = lm[0].y
```
`torso`는 못 구하므로 **모든 임계값은 베이스라인 `W`의 배수**로만 표현한다.
정규화 좌표는 x가 프레임 너비, y가 프레임 높이 기준이라 등방성이 아니다. 카메라를 640×480으로 고정(§9.2)하므로 이 비율은 임계값 초기값에 녹여 두고 코드에서 따로 보정하지 않는다.

**베이스라인** — §8.2와 같이 최근 1초 idle 프레임의 중앙값. 단 `shoulderMid.y / shoulderMid.x / noseY / shoulderW` 4개.

**판정 규칙 (v1, 전부 `TuningProfile`)**

| 동작 | 조건 | 초기 임계값 | 근거 |
|---|---|---|---|
| **Jump** | `baselineShoulderY - shoulderMid.y > T_jump * W` | `T_jump = 0.45` | 전신이 뜨면 어깨도 같이 뜬다 |
| **KneeRaise** (실제 제스처: **ArmRaise**) | 한쪽 손목 `noseY - wristY > T_arm * W` **이고** 반대쪽 손목은 어깨선 아래 | `T_arm = 0.20` | 무릎이 안 보이므로 대체. 한 손만 → 좌우 흔들림/기지개와 구분 |
| **Duck** | `noseY - baselineNoseY > T_duck * W` | `T_duck = 0.80` | 점프 직전 움츠림(≈0.3W)보다 확실히 커야 한다 |
| **StepLeft/Right** | `bodyX - baselineX` 가 `±T_step * W` 밖이면 그 쪽 **존(zone)**. `bodyX = lerp(shoulderMid.x, nose.x, headWeight)` | `T_step = 0.30`, `headWeight = 0.6` | 아래 "존 방식" 참조. 코를 섞는 이유: 노트북 캠 앞에선 실제로 옆으로 걷기보다 **기울이기**가 자연스럽고, 기울이면 머리가 어깨보다 크게 움직인다 (2026-09-18 실측 후 0.50→0.30) |

**사이드 스텝은 임펄스가 아니라 존(zone) 방식이다.** 몸의 x 위치를 `Left / Center / Right` 3개 존으로 나누고, 존이 바뀔 때마다 `StepLeft`/`StepRight`를 한 번 낸다. 왼쪽 존에서 가운데로 돌아오면 `StepRight`가 난다. 이래야 실제 몸 위치와 게임 레인이 1:1로 맞는다. 존 경계에는 히스테리시스(§8.4)를 건다. 베이스라인 x는 **Center 존에 있을 때만** 갱신한다.

**손실 처리** — 필요한 랜드마크 중 하나라도 `visibility < minVisibility`(초기 0.5)면 그 프레임은 해당 동작을 판정하지 않는다(상태 유지). `ok:false`가 `lostTimeout`(초기 1.0s) 넘게 이어지면 자세를 `Grounded`로 되돌리고 HUD에 "인식 끊김"을 띄운다. 게임은 멈추지 않는다.

**전신 모드(§8.3)와의 관계** — `PostureDetector`는 `BodyMode`를 받아 두 규칙 세트 중 하나를 쓴다. 안정화 3종(§8.4)·베이스라인·상태 기계·출력 타입은 공유한다.

---

## 9. Python 앱 사양

### 9.1 구조

```
src/pose/
├─ core/
│  ├─ landmarks.py     # 인덱스 상수, 정규화 계산. 순수 함수만
│  └─ packet.py        # 패킷 직렬화. IO 없음
├─ adapters/
│  ├─ camera.py        # OpenCV VideoCapture
│  ├─ detector.py      # MediaPipe PoseLandmarker 래핑
│  ├─ udp_sink.py      # UDP 송신
│  └─ file_sink.py     # NDJSON 녹화
└─ main.py             # CLI 조립
```

`core/`는 mediapipe도 cv2도 import하지 않는다. Python 쪽에도 같은 의존성 규칙을 얇게 적용한다.

### 9.2 MediaPipe 사용 (필독)

```python
# 올바른 형태
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

options = vision.PoseLandmarkerOptions(
    base_options=mp_python.BaseOptions(model_asset_path="models/pose_landmarker_lite.task"),
    running_mode=vision.RunningMode.VIDEO,   # LIVE_STREAM도 가능
    num_poses=1,
)
landmarker = vision.PoseLandmarker.create_from_options(options)
```

- 모델은 `lite`로 시작한다. `full`/`heavy`는 지연이 커서 프로토타입에 맞지 않는다.
- **`mp.solutions.pose` 는 존재하지 않는다.** 그 형태의 코드가 나오면 잘못된 것이다.
- 카메라 해상도는 **640×480 @30fps**로 고정한다. 올려도 정확도는 거의 안 오르고 지연만 는다.
- `cv2.CAP_DSHOW` 백엔드를 명시하면 Windows에서 카메라 초기화가 빨라진다.

### 9.3 CLI

```bash
uv run pose live                          # 카메라 → UDP 송신
uv run pose live --record recordings/jump.ndjson   # 송신하면서 동시에 녹화
uv run pose replay recordings/jump.ndjson --loop   # 파일을 원래 타임스탬프대로 재생 → UDP
uv run pose live --preview                # 랜드마크 오버레이 창 표시 (디버그)
```

**`replay`는 있으면 좋은 기능이 아니라 필수 기능이다.**
이게 없으면 임계값 하나 바꿀 때마다 자리에서 일어나 점프해야 한다. 이 루프를 수백 번 돌게 되므로, 앉은 채로 튜닝할 수 있는 수단이 프로젝트 전체 일정을 좌우한다. **마일스톤 M2에서 반드시 만든다.**

---

## 10. 구현 순서

각 마일스톤의 DoD를 만족시킨 뒤 다음으로 넘어간다. 앞질러 가지 않는다.

### M0 — 프로젝트 골격
- 저장소 구조(§3) 생성, `.gitignore` / `.gitattributes`(LFS) 작성
- Unity 프로젝트 설정: **Editor → Asset Serialization = Force Text**, **Version Control = Visible Meta Files**
- 5개 asmdef 생성 및 참조 배선(§4)
- VContainer / UniTask / Input System 설치
- `uv init`, `pyproject.toml` 의존성 고정
- **DoD**: Domain asmdef 안의 스크립트에 `using UnityEngine;` 을 넣으면 컴파일 에러가 난다

### M1 — 수직 슬라이스 (가장 중요)
- Python: 카메라 → 랜드마크 → 9개 추출 → UDP 송신
- Unity: UDP 수신 스레드 → 메인 스레드에서 최신 패킷 1개만 읽기 → 화면의 캡슐이 점프
- 점프 판정 하나만. 나머지 3동작은 아직 안 만든다
- **DoD**: 카메라 앞에서 점프하면 화면의 캡슐이 뛴다. HUD에 지연(ms)이 표시된다

> 여기까지 오면 이 프로젝트의 기술적 불확실성은 대부분 사라진다.

### M2 — 녹화 / 재생
- `file_sink.py`, `replay` 명령 구현
- 4개 동작을 각각 10회씩 찍어 `recordings/` 에 저장
- **DoD**: 카메라를 뽑고 `uv run pose replay` 만으로 M1이 동일하게 동작한다

### M3 — 키보드로 게임 완성
- `IPlayerActionSource` 정의, `KeyboardActionSource` 구현
- Domain: `ClearanceRule`, `RunState`
- Application: 구간 생성, 틱 진행, 충돌 판정, 코인, 점수
- Presentation: 프리미티브 오브젝트, `ObjectPool<T>`로 구간·장애물 재사용, HUD
- **DoD**: 키보드만으로 게임이 처음부터 끝까지 플레이된다. 포즈 관련 코드는 하나도 안 켜져 있다

### M4 — 포즈 입력 연결
- `PostureDetector` 상태 기계 구현 (§8)
- `PoseActionSource` 구현, Composition에서 소스를 스왑 가능하게
- 나머지 3동작 판정 추가
- **DoD**: Composition의 설정값 하나만 바꿔서 키보드 ↔ 카메라를 전환할 수 있고, 양쪽 다 정상 동작한다

### M5 — 튜닝과 마감
- 녹화 파일로 임계값 튜닝, 오인식률 측정
- 캘리브레이션 화면 (전신이 프레임에 들어왔는지 안내)
- 지연 목표 검증
- **DoD**: 녹화 세트 기준 4동작 인식률 90% 이상, 종단 지연 이동평균 100ms 이하

---

### 진행 상황 (2026-09-18)

| 마일스톤 | 코드 | 검증 | 남은 것 |
|---|---|---|---|
| M0 | 완료 | batchmode 컴파일 OK, Domain에 `using UnityEngine` 넣으면 CS0246 확인 | — |
| M1 | 완료 | UDP 합성 패킷 → Jump 이벤트 → 지연 표시 (PlayMode 스모크, 사본에서만) | **실제 카메라로 확인** (`uv run pose live`) |
| M2 | 완료 | `replay`가 seq/t를 다시 매겨 송신 | **동작별 10회 녹화** → `recordings/<action>_x10.ndjson` |
| M3 | 완료 | EditMode 61개 통과 (ClearanceRule 32 + 코덱 + 판정기 + 세션) | 씬에 `GameLifetimeScope` 배치(§14.1) 후 플레이 확인 |
| M4 | 완료 | `GameConfig.inputSource` 한 필드로 전환 | 카메라로 4동작 확인 |
| M5 | 도구만 | 녹화 회귀 테스트 하네스, 캘리브레이션 게이트, 인스펙터 실시간 튜닝 | 녹화 세트로 인식률 90% / 지연 100ms 측정 |

---

## 11. 코딩 규칙

### 공통
- **매직 넘버 금지.** 튜닝 값은 `TuningProfile` (ScriptableObject) 한 곳에
- **주석은 "왜"만.** "무엇"은 코드가 말하게 한다
- 한 함수는 한 가지 일만. 20줄을 넘으면 쪼갤 곳이 없는지 본다

### C#
- `#nullable enable` 을 모든 파일에 적용
- Domain/Application의 타입은 **불변**으로. 상태 변경은 새 인스턴스 반환
- **싱글턴 금지.** `static` 가변 상태 금지. `FindObjectOfType` / `GameObject.Find` 금지. 전부 VContainer 생성자 주입으로
- 코루틴 대신 **UniTask**. `async UniTaskVoid` + `CancellationToken`
- `Update()`에서 `new` 하지 않는다. GC 압박을 만들지 않는다
- MonoBehaviour는 **얇게** — 입력 전달과 렌더링만. `if`로 게임 규칙을 판단하면 잘못된 위치에 있는 것이다

### 스레드
- UDP 수신은 별도 스레드. **수신 스레드에서 Unity API를 절대 호출하지 않는다** (즉시 예외)
- 수신 스레드는 `volatile` 최신 패킷 슬롯에 덮어쓰기만 하고, 메인 스레드가 `Update`에서 읽는다. 큐를 쌓지 않는다 (쌓으면 지연이 누적된다)

### Python
- 모든 공개 함수에 타입 힌트
- `core/`는 IO·전역 상태·프레임워크 의존 금지
- `ruff check` / `ruff format` 통과

---

## 12. 테스트 전략

프로토타입이라고 테스트를 전부 생략하지 않는다. **딱 두 군데만 쓴다. 비용 대비 효과가 압도적인 곳이다.**

### 1) `ClearanceRule` 전수 테스트 (EditMode, NUnit)
장애물 4종 × 자세 4종 × 레인 일치/불일치 = 32 케이스. §5 규칙 표를 그대로 옮긴다.
이 규칙은 게임의 재미 자체이고, 여기 버그가 나면 "왜 부딪혔는지" 디버깅하느라 몇 시간이 날아간다.

### 2) `PostureDetector` 회귀 테스트 (EditMode, NUnit)
녹화된 `.ndjson` 파일을 픽스처로 넣고, "jump.ndjson을 먹이면 Jump가 정확히 10회 검출된다"를 검증한다.
임계값을 조정할 때마다 이 테스트를 돌리면 **한 동작을 고치다 다른 동작을 망가뜨리는 사고**를 즉시 잡는다.

이 두 테스트가 가능한 이유는 오직 Domain/Application이 Unity를 모르기 때문이다. §4를 지켜야 하는 실용적 근거다.

**하지 않는 것**: PlayMode 테스트, MonoBehaviour 테스트, UI 테스트, 목(mock) 프레임워크 도입.

---

## 13. 알려진 함정

| 함정 | 대응 |
|---|---|
| `mp.solutions.pose` 코드가 생성됨 | Tasks API로 재작성. §0-4 |
| Python 3.13에서 `pip install mediapipe` 실패 | 3.12로 고정 |
| 씬 파일 병합 충돌 | Force Text + Visible Meta Files, 씬 편집 최소화, 프리팹 우선 |
| 수신 큐가 쌓여 지연 누적 | 큐 쓰지 말고 최신값 1개만 유지 |
| 사용자가 카메라에 가까이 서면 판정 붕괴 | 절대 좌표 금지, `torso` 배수로만 임계값 표현 |
| 경계에서 동작이 떨림 | 히스테리시스 + N프레임 + 쿨다운 3종 전부 |
| 역광에서 인식 실패 | 프로토타입 범위 밖. 단, `ok: false`일 때 게임이 멈추지 않고 "인식 끊김" 표시만 하도록 |
| 튜닝하느라 계속 일어섰다 앉음 | M2를 절대 건너뛰지 않는다 |

---

## 14. 실행 방법

```bash
# 1. 포즈 송신기
cd pose
uv sync
uv run pose live --preview

# 2. Unity
# unity/muhanok-prototype/ 를 Unity Hub에서 열고 Play
# (씬 준비는 §14.1 참조)
```

키보드 모드로 실행하려면 `Composition/GameLifetimeScope`의 입력 소스 설정을 `Keyboard`로 바꾼다.

| 키 | 동작 |
|---|---|
| Space | Jump |
| W | KneeRaise |
| S | Duck |
| A / D | StepLeft / StepRight |

### 14.1 씬 준비 (한 번만)

씬 파일은 에이전트가 편집하지 않는다(§0-3). 게임에 필요한 오브젝트(카메라·조명·플레이어·HUD·구간)는 **전부 런타임에 코드가 만든다.**
씬에 필요한 것은 합성 루트 하나뿐이다.

1. `Assets/Scenes/SampleScene.unity` 를 연다.
2. Hierarchy에서 빈 GameObject를 만들고 이름을 `GameLifetimeScope`로 한다.
3. `Muhanok.Composition.GameLifetimeScope` 컴포넌트를 붙인다.
4. Inspector의 `Game Config` 슬롯에 `Assets/Settings/GameConfig.asset` 을 끌어 넣는다.
5. 템플릿이 만든 `Main Camera` / `Directional Light`는 그대로 둬도 된다 (코드가 있으면 재사용, 없으면 생성).
6. Play.

입력 소스 전환은 `GameConfig.asset`의 `Input Source` 필드(`Keyboard` / `Pose`)로 한다.

### 14.2 사양 밖 결정 기록

사양에 없어서 프로토타입용으로 정한 값. 바꾸려면 여기와 `GameTuning.asset`을 같이 고친다.

| 항목 | 결정 | 이유 |
|---|---|---|
| 점수 공식 | `Score = floor(DistanceTravelled) + Coins × coinScore` (`coinScore = 10`) | "거리 기반 점수"를 가장 단순하게 |
| 자세 지속 시간 | Jump/KneeRaise/Duck 이벤트는 `postureDuration`(0.6s)만큼 자세를 유지한 뒤 `Grounded`로 복귀 | 키보드·포즈 양쪽이 **이벤트**만 내면 되므로 100% 호환 |
| 교도관 거리 | 시작 `chaserGapStart`(10m). 넘어지면 `stumblePenalty`(4m) 감소, 초당 `gapRecovery`(0.5m/s) 회복, 최대 `chaserGapMax` | 몇 번 실수는 만회 가능, 연속 실수는 종료 |
| 속도 | `speedStart` 6m/s에서 `speedGainPerMeter`로 선형 증가, `speedMax` 14m/s | 러너 장르 관례 |
| 장애물 판정 시점 | 플레이어 거리가 장애물 위치를 **넘어서는 틱**에 딱 한 번 판정 (`RunSession.ResolveCrossings`) | 순수 함수 `ClearanceRule`을 그 시점에 한 번 호출. 별도 판정 창 없음 |
| 패킷 파싱 | 범용 JSON 라이브러리 대신 §7 고정 스키마 전용 파서 `PosePacketCodec`(Application) | 의존성 0, 스레드 안전, 녹화 파일과 UDP가 같은 코드 경로 |
| 사이드 스텝 = 존 방식 | §8.6 참조. 키보드 모드는 임펄스(A/D 한 번 = 한 레인) | 몸 위치와 레인을 1:1로 맞추기 위해 |
| 카메라 | 플레이어 뒤·위 3인칭 고정(`(0, 4, -7)`, 15° 내려봄) | 3레인이 다 보이는 가장 흔한 앵글 |
