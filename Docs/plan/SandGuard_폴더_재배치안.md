# SandGuard 폴더 재배치안

작성일: 2026-09-18  
상태: 설계안. 실제 에셋 이동과 코드 수정은 수행하지 않음.

## 1. 목적과 적용 범위

코드와 리소스를 기능 소유자 가까이에 배치한다. Player, Enemy, Tower 등 개별 요소는 자신의 동작과 전용 연출을 소유하고, Integration은 요소들을 연결하는 동작을, Shared는 공통 계약·기반 구현·재사용 리소스를 소유한다.

이 문서는 폴더와 자식 폴더 수준의 목표 구조 및 기존 폴더의 이동 방향을 제시한다. 파일별 이동 목록은 포함하지 않는다. 현재 폴더를 조사한 결과를 바탕으로 작성했으며, 사용처와 의존성 확인이 필요한 부분은 조건을 명시했다.

모든 하위 폴더를 미리 만들지 않는다. 아래 트리는 배치 가능한 위치를 정의하며, 실제 콘텐츠가 있는 폴더만 생성한다. 특히 Cutscene과 ScreenEffects는 향후 기능 추가 시 사용할 예약 위치다.

## 2. 기본 배치 규칙

- 개별 요소 전용 코드, 프리팹, 애니메이션, 사운드, VFX는 해당 요소 안에 둔다.
- Runtime은 실행 코드, Editor는 제작·설정 도구, Data는 설정 에셋, Tests는 검증 코드와 검증용 콘텐츠다.
- 기존 Runtime 명칭과 Art 묶음을 유지한다. Scripts로 일괄 변경하거나 모든 Art를 파일 형식별로 해체하지 않는다.
- 한 효과나 캐릭터에 딸린 재질·텍스처는 그 효과나 캐릭터 폴더에 함께 둬도 된다.
- 공유하는 소비자의 조합별 폴더는 만들지 않는다. PlayerEnemyShared 같은 교집합 폴더를 사용하지 않는다.
- 여러 곳에서 참조한다고 반드시 Shared로 옮기지는 않는다. 명확한 소유자가 있으면 그 소유자에게 남긴다.
- Shared에 들어갈 수 있는 책임을 제한한다. 개별 캐릭터 행동, 게임 진행 조율, 특정 레벨 연출은 제외한다.
- 아래 목표 의존 방향은 재배치의 설계 기준이다. 현재 코드가 이미 모두 준수한다는 뜻은 아니다.

```text
Integration → 개별 요소 → Shared
Integration ──────────→ Shared
```

Integration 내부의 UI, Audio, ScreenEffects 등은 명시적인 인터페이스를 통해 협력할 수 있다. 개별 요소가 Integration 구현을 직접 요구한다면 계약을 아래 계층에 두고 연결은 Integration에서 수행한다. 폴더 이동과 이런 코드 책임 변경은 별도 단계로 진행한다.

## 3. 최상위 목표 구조

```text
Assets/
├─ _Project/
│  ├─ Player/
│  ├─ Enemy/
│  ├─ Tower/
│  ├─ Facility/
│  ├─ SkillTree/
│  ├─ Level/
│  ├─ Integration/
│  ├─ Shared/
│  └─ Settings/
├─ Plugins/                   # 외부 패키지: 최초 재배치에서는 유지
├─ TextMesh Pro/              # 외부 패키지: 최초 재배치에서는 유지
└─ 기타 외부 패키지/          # 출처·경로 의존성을 확인하기 전에는 유지

Docs/                        # 기존 문서 유지
Tools/                       # 기존 프로젝트 외부 도구 유지
Packages/                    # Unity 패키지 관리 영역 유지
ProjectSettings/             # Unity 설정 영역 유지
```

외부 패키지를 일괄적으로 ThirdParty로 옮기는 작업은 포함하지 않는다. Resources 아래의 외부 VFX 패키지도 우선 현 위치를 유지한다. 복구본과 임시 작업물은 자동 삭제하지 않고 별도로 사용 여부를 확인한다.

## 4. 개별 요소의 상세 구조

### Player

```text
Player/
├─ Runtime/
│  └─ Effects/                # 기존 스킬 효과 구현 묶음 유지
├─ Editor/
│  └─ AvatarAudit/
├─ Art/
│  ├─ Protagonist/
│  │  ├─ CombatAnimations/
│  │  └─ MobilityAnimations/
│  └─ Lamp/
├─ Prefabs/
├─ Data/
├─ Input/                     # 플레이어 전용 입력 에셋인 경우
├─ VFX/
├─ Audio/
│  ├─ Cues/
│  └─ Clips/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/
   └─ Fixtures/               # 테스트 전용 대상과 리소스
```

현재 Generated의 실제 게임용 프리팹과 테스트·아트 확인용 씬을 분리한다. 생성되었다는 사실보다 사용 목적을 기준으로 배치한다. 다만 생성 도구의 출력 경로를 함께 수정할 수 있는 단계에서 수행한다.

### Enemy

```text
Enemy/
├─ Runtime/
├─ Editor/
├─ Art/
│  ├─ Characters/
│  │  ├─ Assassin/
│  │  ├─ Chief/
│  │  ├─ HammerBrute/
│  │  ├─ ShieldGuard/
│  │  └─ Swordsman/
│  ├─ ChiefBomb/
│  │  └─ Materials/
│  └─ Equipment/
├─ Prefabs/
├─ Data/
├─ VFX/
├─ Audio/
│  ├─ Cues/
│  └─ Clips/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/
   └─ Fixtures/
```

적 자신의 머리 위 체력 표시나 피격 반응은 Enemy에 남긴다. HUD의 보스 정보 표시처럼 다른 시스템과 연결되는 표현은 Integration/UI가 소유한다. 타워와 적을 실제로 함께 실행하는 테스트는 Integration/Tests로 분류한다.

### Tower

```text
Tower/
├─ Runtime/
│  ├─ Anubis/
│  ├─ Cobra/
│  └─ Coffin/
├─ Editor/
├─ Art/
│  ├─ Models/
│  ├─ Materials/
│  ├─ Animations/
│  └─ Summons/                # 타워가 소유한 소환체 외형
├─ Prefabs/
├─ Data/
├─ VFX/
├─ Audio/
│  ├─ Cues/
│  └─ Clips/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/
   └─ Fixtures/
```

타워의 탐색·발사·공격·소환·공격 연출을 담당한다. Facility와의 경계는 타워의 전투 동작과 시설의 설치·관리 책임으로 나눈다. 실제 프리팹에 두 기능이 함께 붙어 있어도 에셋은 한쪽에서만 소유하며, 참조를 위해 복제하지 않는다.

### Facility

```text
Facility/
├─ Runtime/
├─ Editor/
├─ Art/
│  ├─ Tower/                  # 설치·건설 표현에 해당하는 경우 유지
│  └─ UI/                     # 시설 전용 시각 리소스
├─ Prefabs/
│  └─ Towers/                 # 시설 카탈로그가 소유한 조립 프리팹
├─ Data/
├─ VFX/
├─ Audio/
│  ├─ Cues/
│  └─ Clips/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/
   └─ Fixtures/
```

설치·건설·수리·강화 등 시설 기능을 소유한다. 현재 Art/Tower와 Generated/Towers는 이름만 보고 Tower로 일괄 이동하지 않고 실제 역할을 확인한다.

### SkillTree

```text
SkillTree/
├─ Core/                      # 기존 코어 어셈블리 유지
├─ Runtime/
├─ Editor/
├─ Art/
├─ Prefabs/
├─ Data/
├─ Resources/                 # 기존 동적 로드가 필요한 동안 유지
│  └─ SandGuardLampSkin/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/
   └─ Fixtures/
```

현재 SandGuardSkillTree/Assets/SkillTree 중첩을 제거한다. 스킬 트리 자체의 창과 노드 표현은 여기 남기고, 플레이어·HUD와의 연결은 Integration/UI에서 담당한다.

### Level

```text
Level/
├─ Runtime/                   # 레벨 자체의 실행 기능
├─ Editor/
├─ Data/
├─ Navigation/
├─ Art/
│  ├─ Materials/
│  ├─ Models/
│  ├─ Meshes/
│  ├─ Terrain/
│  ├─ Decorations/
│  └─ TrimSheet/
│     ├─ Editor/
│     ├─ Materials/
│     ├─ Meshes/
│     ├─ Models/
│     ├─ Prefabs/
│     └─ Textures/
├─ Prefabs/
├─ Scenes/                    # 실제 게임 레벨 씬
├─ Temple/
│  ├─ Art/
│  │  ├─ Architecture/
│  │  ├─ ArchitectureV2/
│  │  ├─ Materials/
│  │  └─ Models/
│  ├─ Editor/
│  ├─ Prefabs/
│  ├─ Terrain/
│  ├─ VFX/
│  │  └─ Sandstorm/
│  ├─ Audio/
│  ├─ Cutscenes/              # 해당 레벨 전용 콘텐츠가 생기면 생성
│  └─ Tests/
│     └─ Scenes/
├─ HiddenCanyon/
│  ├─ Data/
│  ├─ Editor/
│  ├─ Art/
│  │  ├─ Materials/
│  │  └─ Meshes/
│  ├─ Prefabs/
│  └─ Scenes/
├─ SpiralBlockout/
│  ├─ Data/
│  ├─ Editor/
│  ├─ Art/
│  │  ├─ Materials/
│  │  └─ Models/
│  ├─ Prefabs/
│  └─ Scenes/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/                 # 갤러리·블록아웃 검증 씬
   ├─ Fixtures/
   └─ Diagnostics/
      ├─ Runtime/
      └─ Editor/
```

현재 DesertTowerLevels와 Resources/DesertTowerLevels에는 같은 이름의 자식 폴더가 있다. 같은 이름을 중복 에셋으로 단정하지 않고 GUID·사용처·로드 방식을 확인한 뒤 합친다. 개별 레벨의 Scenes와 공통 Scenes 중 실제 소유 위치는 하나만 사용한다.

## 5. Integration 상세 구조

```text
Integration/
├─ UI/
│  ├─ HUD/
│  │  ├─ Runtime/
│  │  │  ├─ View/
│  │  │  └─ Binder/
│  │  ├─ Editor/
│  │  ├─ Art/
│  │  │  ├─ Sprites/
│  │  │  ├─ Materials/
│  │  │  └─ Shaders/
│  │  ├─ Prefabs/
│  │  └─ Tests/
│  │     ├─ Debug/
│  │     └─ Scenes/
│  ├─ MainMenu/
│  │  ├─ Runtime/
│  │  ├─ Art/
│  │  ├─ Prefabs/
│  │  └─ Scenes/
│  ├─ HowToPlay/
│  │  ├─ Runtime/
│  │  ├─ Editor/
│  │  ├─ Art/
│  │  │  └─ Pages/
│  │  └─ Prefabs/
│  ├─ GameResult/
│  │  ├─ Runtime/
│  │  ├─ Art/
│  │  ├─ Prefabs/
│  │  └─ Tests/
│  │     └─ Scenes/
│  ├─ Audio/
│  │  ├─ Cues/
│  │  └─ Clips/
│  └─ Tests/
│     ├─ EditMode/
│     └─ PlayMode/
├─ GameFlow/
│  ├─ Runtime/
│  ├─ Data/
│  └─ Tests/
├─ Waves/
│  ├─ Runtime/
│  ├─ Editor/
│  ├─ Data/
│  ├─ Prefabs/
│  ├─ Audio/
│  │  └─ Cues/
│  └─ Tests/
│     ├─ EditMode/
│     ├─ PlayMode/
│     └─ Scenes/
├─ LevelSetup/
│  ├─ Runtime/                # 초기화, 플레이어·적·시설·레벨 연결
│  ├─ Editor/
│  ├─ Data/                   # 통합 구성용 카탈로그 등
│  ├─ Prefabs/
│  └─ Tests/
│     ├─ EditMode/
│     ├─ PlayMode/
│     └─ Scenes/
├─ Audio/
│  ├─ Runtime/                # 공통 재생·볼륨·음소거 관리
│  ├─ Editor/
│  │  └─ Synth/
│  ├─ Mixers/
│  ├─ Music/
│  ├─ Synth/
│  └─ Tests/
│     ├─ EditMode/
│     ├─ PlayMode/
│     └─ Resources/           # 테스트 로드 규칙 확인 후 유지
├─ Cutscene/                  # 예약: 실제 기능 추가 시 생성
│  ├─ Runtime/
│  ├─ Editor/
│  ├─ Timelines/
│  ├─ Prefabs/
│  └─ Tests/
│     └─ Scenes/
├─ ScreenEffects/             # 예약: 컷씬 외에서도 공유할 때 분리
│  ├─ Runtime/
│  ├─ Art/
│  │  ├─ Materials/
│  │  └─ Shaders/
│  ├─ Prefabs/
│  └─ Tests/
│     └─ Scenes/
└─ Tests/
   ├─ EditMode/
   ├─ PlayMode/
   ├─ Scenes/
   ├─ Fixtures/
   └─ LoadTest/
      ├─ Runtime/
      ├─ Editor/
      ├─ Scenes/
      └─ Data/
```

- GameFlow는 시작·종료·승패 등 전체 흐름을 담당한다.
- Waves는 웨이브 진행과 적 생성 연결을 담당한다. EnemyPool의 구체적인 사용 범위를 확인하여 Enemy 전용 기반인지 Waves의 생성 자원 관리인지 최종 결정한다.
- LevelSetup은 레벨과 개별 요소를 연결하는 부트스트랩·어댑터·카탈로그를 담당한다. 레벨 지형과 환경 아트는 포함하지 않는다.
- Audio에는 재생 관리 기능을 두고, 요소별 큐와 클립은 해당 요소에 둔다. 여러 곳에서 동일하게 쓰는 원본 음원은 Shared/Art/Audio에 둔다.
- Cutscene은 재생·종료·스킵과 제어권 복구를 담당한다. 특정 레벨 전용 타임라인과 효과는 해당 Level에 둔다.
- ScreenEffects는 페이드·플래시 등 전체 화면 효과의 실행만 담당한다. 효과 이후의 레벨 전환이나 승패 처리는 호출자가 결정한다.
- 기존 어셈블리에 섞인 책임을 위 폴더로 나누는 작업은 의존성 확인 후 진행한다. 위치 변경과 동시에 어셈블리를 무조건 분할하지 않는다.

## 6. Shared와 Settings 상세 구조

```text
Shared/
├─ Gameplay/
│  ├─ Contracts/              # 요소 사이에서 공유하는 인터페이스·계약
│  ├─ Data/                   # 공통 값·요청·결과 데이터
│  ├─ Runtime/                # 기존 공통 판정·규칙 구현
│  └─ Tests/
│     ├─ EditMode/
│     └─ PlayMode/
├─ VFX/
│  ├─ Runtime/
│  ├─ Editor/
│  ├─ Prefabs/
│  ├─ Materials/
│  ├─ Meshes/
│  ├─ Shaders/
│  ├─ Textures/
│  └─ Tests/
│     ├─ EditMode/
│     ├─ PlayMode/
│     └─ Scenes/              # 공용 효과 쇼케이스
└─ Art/
   ├─ Materials/
   ├─ Textures/
   ├─ Shaders/
   ├─ PhysicsMaterials/
   ├─ Fonts/
   ├─ MotionLibrary/
   └─ Audio/
      ├─ Clips/
      └─ Placeholder/

Settings/
├─ Rendering/
└─ Input/                     # UI까지 포함하는 전역 입력 에셋인 경우
```

Shared/Gameplay는 기존 공통 계약 계층을 보존하기 위한 실용적인 범위다. 엄격한 FSD의 기술적 Shared와 동일하지 않다. 전투·설치·게임 상태의 공통 계약을 모두 세부 최상위 모듈로 분리하지 않는다.

Shared/VFX에 개별 요소 전용 효과를 모으지 않는다. Temple의 모래폭풍 등 환경 전용 효과는 Level 쪽으로 배치한다. 풀링 코드도 실제로 VFX 전용이면 이곳에 유지하며, 이름만 보고 범용 기반으로 승격하지 않는다.

Settings는 요소의 행동 설정이 아니라 프로젝트 전체의 렌더링·입력 에셋을 위한 영역이다. 개별 능력치와 스킬 설정은 요소별 Data에 둔다.

## 7. 현재 폴더와 목표 폴더 대응

경로는 Assets를 기준으로 한다. 여러 목적지가 있는 항목은 내부 콘텐츠의 소유권에 따라 분리한다.

| 현재 폴더 | 목표 위치 / 처리 |
|---|---|
| Player | _Project/Player. Runtime·Editor·Art·Tests 유지, Generated는 용도별 분리 |
| Enemy | _Project/Enemy. 통합 테스트는 Integration/Tests로 분리 |
| Facility | _Project/Facility. Generated는 Prefabs·Data·Tests/Scenes로 분리 |
| 3.Script/Tower | _Project/Tower/Runtime |
| 3.Script/UI/HUD | _Project/Integration/UI/HUD |
| 3.Script/UI/HowToPlay | _Project/Integration/UI/HowToPlay |
| 3.Script/MainMenu | _Project/Integration/UI/MainMenu/Runtime |
| 3.Script/Enemy (Temporary) | 현재 Enemy와의 참조 관계 확인 후 분류. 자동 삭제·병합 금지 |
| SandGuardSkillTree/Assets/SkillTree | _Project/SkillTree |
| SandGuardSkillTree/Tests | _Project/SkillTree/Tests |
| GameFlow | _Project/Integration/GameFlow |
| Waves | _Project/Integration/Waves |
| DesertTowerLevelIntegration | _Project/Integration/LevelSetup 중심. 웨이브는 Waves, 결과 화면은 UI/GameResult로 분리 |
| DesertTowerLevels | _Project/Level. 공용 레벨 콘텐츠와 개별 레벨 콘텐츠 구분 |
| Resources/DesertTowerLevels | _Project/Level. Resources 로드 여부와 동명 폴더 충돌 확인 필수 |
| Resources/DesertTowerLevelEditor | _Project/Level/Editor |
| Resources/DesertTowerLevelDiagnostics | _Project/Level/Tests/Diagnostics |
| TempleArt | _Project/Level/Temple 중심. 공통 레벨 제작 도구는 Level/Editor |
| DesertTemple | 출처 확인 후 자체 제작 도구는 Level/Editor, 예제는 Level/Tests/Scenes 또는 Tower/Tests로 분류 |
| Decorations | 자체 레벨 장식이면 _Project/Level/Art/Decorations |
| GoldenArsenal | 자체 관리 에셋의 사용처 확인. 적 전용 장비는 Enemy/Art/Equipment, 공유 에셋은 소유 범위 확인 후 배치 |
| MinionVisuals | 타워 소환체 전용이면 _Project/Tower/Art/Summons. 제작 도구는 Tower/Editor |
| MotionLibrary | 공유 모션 원본이면 _Project/Shared/Art/MotionLibrary. 외부 원본 구조가 필요하면 유지 |
| DesertTowerVFX | 공용 구현·도구는 _Project/Shared/VFX, 레벨 전용 효과는 Level로 분리 |
| Resources/VFX | _Project/Shared/VFX 중심. TempleSandstorm은 Level/Temple/VFX/Sandstorm 후보 |
| Resources/Interfaces | _Project/Shared/Gameplay/Contracts |
| Resources/Combat | _Project/Shared/Gameplay의 계약·데이터·구현에 배치. 기존 어셈블리 경계 보존 |
| Resources/Gameplay | _Project/Shared/Gameplay |
| 8.Audio/Runtime, Editor, Synth, Tests | _Project/Integration/Audio의 대응 폴더 |
| 8.Audio/Cues | Player·Enemy·Facility·Level·Integration/UI·Waves 등 소유자별 Audio/Cues |
| 8.Audio/AudioResource, Placeholder | 공용이면 Shared/Art/Audio. 외부 원본 및 라이선스 묶음 유지 |
| 2.Model/Prefabs/HUD, UI | _Project/Integration/UI의 해당 기능별 Prefabs |
| 2.Model/Prefabs/Level | 지형·장식은 Level/Prefabs, 통합 조립용은 Integration/LevelSetup/Prefabs |
| 2.Model/PIpeLine | _Project/Settings/Rendering |
| 4.Sprite | 소유 기능별 Art로 분리. HUD는 Integration/UI/HUD/Art, 시설 전용은 Facility/Art/UI |
| 5.Animation/Core | 코어의 실제 기능 소유권 확인 후 해당 요소 Art/Animations. 이름만으로 GameFlow에 넣지 않음 |
| 6.Materials | 공용은 Shared/Art, 레벨 전용은 Level/Art, 요소 전용은 해당 요소 Art |
| 7.PhysiceMaterials | 공용은 Shared/Art/PhysicsMaterials, 전용은 해당 요소 Art |
| 9.Font | 프로젝트 관리 폰트는 Shared/Art/Fonts. 라이선스·원본 묶음 유지 |
| 10.Input | 플레이어 전용이면 Player/Input, 전역 입력이면 Settings/Input |
| 1.Scene | 실제 레벨은 Level/Scenes, 메뉴는 Integration/UI/MainMenu/Scenes, 검증 씬은 담당 Tests/Scenes |
| LoadTest | _Project/Integration/Tests/LoadTest |
| _Recovery | 복구본으로 우선 유지. 정리·삭제는 별도 판단 |
| SimpleSky, Plugins, TextMesh Pro | 외부 패키지 여부 및 경로 의존성 확인 전 현 위치 유지 |
| Resources 아래 외부 VFX 패키지 | 최초 재배치에서 유지. 프로젝트 자체 효과와 구분 |

코어 관련 리소스는 현재 여러 위치에 분산되어 있다. 이번 폴더 조사만으로 Facility·Level·별도 Core 중 하나를 확정하지 않는다. 실제 컴포넌트와 사용처를 확인한 뒤 하나의 소유 위치를 선택한다.

## 8. 테스트 배치와 어셈블리 보존

- 단일 요소 테스트는 해당 요소의 Tests에 둔다.
- 여러 실제 요소가 협력하는 테스트는 Integration/Tests에 둔다.
- 공통 계약·규칙 테스트는 Shared/Gameplay/Tests에 둔다.
- 직접 실행하는 조작감·아트·VFX 확인 씬은 소유자의 Tests/Scenes에 둔다.
- 테스트 전용 프리팹과 더미 콘텐츠는 Tests/Fixtures에 둔다.
- EditMode와 PlayMode 분류는 실제 테스트 실행 환경을 기준으로 한다. 기존 테스트 어셈블리의 설정을 확인하지 않고 이름만으로 이동하지 않는다.
- 폴더 이동 때문에 코드가 다른 어셈블리에 포함되지 않도록 기존 어셈블리 정의와 참조를 추적한다.
- 기존 Editor 하위의 테스트를 EditMode로 옮길 때 에디터 전용 컴파일 설정을 유지한다.

## 9. Resources 및 생성 콘텐츠 처리

Unity의 Resources는 일반 분류 폴더가 아니라 런타임 로드에 영향을 주는 위치다. 코드와 도구가 현재 Resources 아래 있다는 이유만으로 에셋까지 전부 같은 방식으로 이동하지 않는다.

1. 동적 로드 대상과 Resources 기준 상대 경로를 조사한다.
2. 동적 로드를 유지할 에셋은 새 소유자 아래 Resources 폴더에 두되, 로더가 사용하는 상대 경로를 보존하거나 함께 수정한다.
3. Inspector 등 직접 참조로 전환할 콘텐츠는 로드 방식 변경을 별도로 검증한다.
4. 여러 Resources 폴더 사이의 동일 상대 경로 충돌도 확인한다.

Generated는 일괄 삭제하지 않는다. 실제 게임용 에셋은 Prefabs·Data·Art에, 검증 콘텐츠는 Tests에 배치한다. 도구가 전체 출력 디렉터리를 지우고 다시 만드는 방식이라면 수동 관리 에셋과 섞지 않고 전용 생성 폴더를 유지할 수 있다.

## 10. 실행 순서와 완료 기준

1. 현재 변경 상태, 씬·프리팹 참조, 어셈블리, Resources 로드, 문자열 경로를 기록한다.
2. Player·Enemy·Facility처럼 이미 묶인 영역을 폴더 단위로 이동한다. 내부 책임 분리는 보류한다.
3. UI·Tower·SkillTree처럼 흩어진 영역을 모은다.
4. Level·Shared·Audio는 소유권과 동적 로드 경로를 확인하며 이동한다.
5. Generated와 테스트 콘텐츠를 분류하고 생성·검증 도구의 출력 경로를 수정한다.
6. 필요하다면 Integration 내부 책임 분리와 의존성 개선을 별도 변경으로 수행한다.

실제 이동에는 원래 메타데이터와 GUID를 보존한다. 에디터 도구에 직접 적힌 Assets 경로, 빌드 씬 경로, 문서와 외부 도구의 경로도 함께 갱신한다. 폴더를 옮기는 것만으로 직렬화 참조·생성 도구·동적 로드가 모두 안전하다고 간주하지 않는다.

완료 기준은 Unity 컴파일 오류 없음, 주요 씬의 누락 스크립트·참조 없음, 관련 기존 자동 테스트 통과, 생성 도구의 새 경로 사용, Resources 로드 확인, 게임 진입·플레이어·적·타워·웨이브·HUD·오디오의 주요 흐름 확인이다.
