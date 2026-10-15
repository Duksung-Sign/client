<div align="center">

# 🐱 Meowgic (묘우직)

**수어와 AI의 만남 - 수어 인식 기반 턴제 RPG 게임**

![Unity](https://img.shields.io/badge/Unity-2022.3_LTS-000000?logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![MediaPipe](https://img.shields.io/badge/MediaPipe-Holistic-0097A7)
![TensorFlow](https://img.shields.io/badge/TensorFlow-FF6F00?logo=tensorflow&logoColor=white)

[![묘우직 시연 영상](https://media.discordapp.net/attachments/1358614497753043097/1560227108721004634/4_.png?ex=6ad1f23a&is=6ad0a0ba&hm=4083fd074a37f1babaa675aa7544bdbb024e2e4da358e6324ac5b4bae1eb32ae&=&format=webp&quality=lossless&width=1536&height=1111)](https://www.youtube.com/watch?v=pl9M94u0p54)

▲ 이미지를 클릭하면 시연 영상으로 이동합니다.

</div>

> [!NOTE]
> **프로젝트 상태: 서비스 종료**
> 이 저장소는 Meowgic의 **Unity 클라이언트** 코드입니다.
> 수어 인식을 담당하던 AI 서버(AWS EC2)가 현재 운영되지 않아 **게임을 직접 플레이할 수 없습니다.**
> 실제 플레이 모습은 위의 [시연 영상](https://www.youtube.com/watch?v=pl9M94u0p54)에서 확인할 수 있습니다.

## 프로젝트 개요

**Meowgic(묘우직)** 은 Unity 기반 2D 턴제 RPG 게임입니다.

청각장애인이 겪는 언어적 소외와 비장애인의 수어 교육 접근성 부족이라는 사회적 격차를 해소하고자 기획했습니다. 게임이라는 즐거운 매체를 통해 수어 학습의 진입장벽을 낮추고, 지속적인 동기를 부여하며 소리와 침묵의 세계를 연결하는 것을 목표로 합니다.

MediaPipe Holistic을 활용한 실시간 수어 인식 시스템과 전통적인 RPG 게임플레이를 결합했습니다. 플레이어는 수어 동작으로 전투 스킬을 발동하며, 스토리 중심의 어드벤처를 경험합니다.

- **개발 기간**: 2025.05 - 2025.10
- **개발 환경**: Unity 2022.3 LTS, C#

## 게임 소개

### 시놉시스

네 손가락 고양이들이 사는 세상, **묘우나라**.
'묘우직'은 그곳에서 유일하게 다섯 손가락 고양이로 태어난 **'미오'** 의 이야기입니다.

평범함과 조금 다른 그 모습 때문에 '미오'는 무리의 따돌림 속에서 외롭게 자라나지만, 운명은 그에게 특별한 힘, **'묘우'라는 마법**을 내려줍니다.

'묘우직'에서는 '미오'가 몬스터의 비밀을 추적하며 맞서는 모험 속에서 시선과 편견, 따뜻한 도움 등을 마주하고 성장하는 스토리를 담았습니다.

### 캐릭터

| 캐릭터 | 역할 | 설명 |
| --- | --- | --- |
| **미오** | 주인공 | 네 손가락 고양이 나라에서 태어난 다섯 손가락 고양이 |
| **체다** | 주인공의 친구 | 밝고 다정한 성격으로, 주인공을 아껴주는 유일한 친구 |
| **튜나** | 주인공의 조력자 | |

### 주요 기능

- **실시간 수어 인식 전투**: 웹캠 영상에서 손·얼굴·포즈를 추적해 수어 동작을 스킬로 변환
- **턴제 RPG 전투**: 수어 입력으로 공격과 스킬을 선택하는 턴제 전투
- **스토리 모드**: 미오의 성장 서사를 따라가는 스토리 중심 어드벤처
- **수어 학습**: 게임 플레이 자체가 수어 동작 연습이 되도록 설계

## 시스템 구성도

```
┌──────────────── Client ────────────────┐         ┌──────────── AI Server (AWS EC2) ────────────┐
│  Unity (C#)  ·  Arduino  ·  MediaPipe  │ ──────► │  FastAPI  ·  Keras / TensorFlow             │
│  - 게임 로직 / UI                       │ json /  │  - 수어 동작 분류 모델 추론                   │
│  - 웹캠 입력 & 랜드마크 추출             │ REST API│                                             │
│                                        │ ◄────── │                                             │
└────────────────────────────────────────┘         └─────────────────────────────────────────────┘
```

1. 클라이언트가 웹캠 영상에서 **MediaPipe Holistic**으로 랜드마크를 추출
2. 추출한 좌표 데이터를 JSON 형태로 AI 서버에 전송
3. 서버의 TensorFlow(Keras) 모델이 수어 동작을 분류해 결과 반환
4. 클라이언트가 결과를 받아 해당 스킬을 발동


## 기술 스택

| 분류 | 기술 |
| --- | --- |
| **게임 엔진** | Unity 2022.3 LTS (C#) |
| **AI/ML** | MediaPipe Holistic, TensorFlow (Python 서버) |
| **서버** | FastAPI, AWS EC2 |
| **통신** | WebSocket (WebSocketSharp), HTTP (UnityWebRequest) |
| **UI/UX** | Unity UI, TextMeshPro, DOTween |
| **데이터** | ScriptableObject, CSV 파싱, PlayerPrefs |
| **디자인 패턴** | 싱글톤, 이벤트 시스템, FSM, 인터페이스, 옵저버 |


## 🧩 클라이언트 살펴보기

서버 없이 게임 실행은 불가능하지만, Unity 에디터에서 프로젝트를 열어 코드와 씬 구성을 확인할 수 있습니다.

```bash
git clone https://github.com/Duksung-Sign/client.git
```

1. Unity Hub에서 `meowgic` 폴더를 프로젝트로 추가
2. **Unity 2022.3 LTS**로 열기
3. `Assets/` 아래에서 씬, 스크립트, 프리팹 확인

> ⚠️ 수어 인식 전투 등 서버 연동 기능은 AI 서버가 닫혀 있어 정상 동작하지 않습니다.

## 👥 팀원

덕성여자대학교 **묘우묘우**

남서하 · 나예원 · 이재희 · 임지원 · 전하경
