# UI 없는 VFX 8종

생성: `DesertTower > VFX > Build Requested Set (8, No UI)`

Showcase: `DesertTower > VFX > Showcase > Open Showcase Scene + Play`. 앞쪽 `NEW 01`~`NEW 08`에 아래 순서로 모여 있다. 시설 정지는 5초 유지 후 2초 정상 상태를 반복하며, 경험치 입자는 계속 남는다. 전시 이름은 씬 안내판이며 VFX 프리팹에는 포함되지 않는다.

실제 Unity 렌더 확인: `DesertTower > VFX > Preview Requested Set (8, No UI)` → `Docs/vfx-preview/`. 현재 편집 씬을 바꾸지 않는 별도 프리뷰 씬을 사용한다. 개별 이미지의 번호는 아래 순서와 같다.

| 번호 | Prefabs/ 파일 | 배치 기준 | 재생 및 반환 권장 |
|---|---|---|---|
| 1 | VFX_Shield_Front_Guard | 방패 적중점, +Z가 방패 바깥 | 0.25초 표면 육각 섬광, 0.4초 파편. 1초 뒤 반환 |
| 2 | VFX_Facility_Hit | 시설 적중점 | 주황·흰 섬광, 사암 파편. 1.5초 뒤 반환 |
| 3 | VFX_Player_Hit | 플레이어 적중점 | 붉은·흰 파열. 1.5초 뒤 반환 |
| 4 | VFX_Demolition_Bomb_Explosion | 폭탄 착지 지면, +Y가 위 | 붉은 충격파 2겹, 주황 파편, 검은 연기. 2.5초 뒤 반환 |
| 5 | VFX_Facility_Disabled_Loop | Tower (Cobra) 루트와 일치, 자식 로컬 위치 0 | 전기와 붉은 입자 루프. 게임의 정지 상태가 끝나면 반환 |
| 6 | VFX_Core_Damage_Flash | 코어 발밑, 중심 y=1.2 기준 | 붉은 피격 섬광과 바닥 충격파. 2초 뒤 반환 |
| 7 | VFX_Experience_Mote | 지면, 큐브 중심 y=0.35 | 금빛 큐브·후광 유지. 획득 처리 시 반환 |
| 8 | VFX_LevelUp | 플레이어 발밑 | 금색 링 3겹과 나선 입자. 2초 뒤 반환 |

모두 월드 공간 VFX이며 Canvas, 정지 아이콘, 상승 화살표, 숫자, 타이머를 포함하지 않는다. 기존 별도 `VFX_Player_Hit_Screen`은 이번 묶음에 포함하지 않는다.

## 연결

일회성 이펙트는 기존 `VfxOneShot` / `PrefabPool`로 스폰하고 위 권장 시간 뒤 반환한다. 정지 루프는 시설 루트와 일치하도록 자식으로 스폰하고 `VfxDisabledVisual.BindTarget(시설 Transform)`을 호출하면 시설 머티리얼을 임시로 어둡게 탈색한다. 비활성화/반환 시 원래 머티리얼로 복원한다. 같은 시설에 중복 바인딩하지 말고 기존 인스턴스 하나의 정지 시간을 갱신한다. 별도 Light 컴포넌트의 점등 및 공격 중단은 시설 코드가 관리한다. 다른 머티리얼 교체·피격 플래시 기능과의 조합은 연결 후 확인한다.

정지 루프는 `Assets/2.Model/Prefabs/Tower (Cobra).prefab`의 기본 스케일을 기준으로 한다. 보이는 메시 경계는 크기 (3, 3.4, 3), 중심 (0, 3.2, 0)이며 숨겨진 사거리 렌더러는 제외한다. 코브라 루트에 자식으로 붙이고 localPosition=(0,0,0), localRotation=identity, localScale=(1,1,1)로 사용한다. 지면으로 따로 내리지 않는다. 중심 (0, 3.2, 0), 반지름 2.21인 가상 구체 표면에 주 경로 5개와 분기 10개를 배치한다. 모델 표면을 추적하지 않는다. 재생성 메뉴는 `DesertTower > VFX > Build Facility Disabled (Cobra Size)`이다. 루프에는 5초 카운트다운이나 자동 정지 로직이 없다. 실제 철거 폭탄의 5초 정지 상태가 끝나는 시점에 게임 코드가 반환한다.

주 경로는 0.32초 동안 방전이 이동하고 경로 길이의 25%만 잔광으로 남는다. 분기는 주 방전이 분기점에 도착할 때 0.11초 동안 재생된다. 각 경로는 0.95초 주기로 서로 시차를 두고 반복하며, 지나가지 않는 경로 부분은 그리지 않는다. 흰색 중심·붉은 잔광·선두의 짧은 스파크로 구성한다. `VfxLightningPath`의 TravelTime, Tail, Period, Phase, Delay로 조정한다.

루트를 선택하면 `VfxLightningGuide`가 Scene 뷰에 구체와 전기 경로 Gizmo를 그린다. 실제 게임에서는 가이드가 표시되지 않는다. Game 뷰에서 개발용 Gizmos 표시를 켰다면 끄고 확인한다. Unity 렌더 미리보기는 `Docs/vfx-preview/Facility_Disabled_Lightning.gif`, 원본 24프레임은 `Docs/vfx-preview/Lightning/`에 있다.

경험치 프리팹은 외형이다. 적 사망 시 드롭 생성, 경험치 값, 획득 범위 및 이동, 실제 경험치 지급은 별도 게임플레이 연결 작업이다. 이번 작업은 8종 프리팹 제작이며 자동으로 전투 이벤트를 연결하지 않는다.

## 편집

`Editor/RequestedVfxBuilder.cs`에 신규 4종 제작 규칙, `CombatVfxBuilder`에 플레이어 피격, `ImpactVfxBuilder`에 코어 피격, `ExperienceMoteBuilder`에 경험치, `GoldVfxBuilder`에 레벨 업 규칙이 있다. `Build All`에도 포함된다. 구체 방전은 `VfxLightningPath`가 재생하므로 Play 모드 또는 전용 프리뷰 메뉴에서 확인한다. `VfxArcPulse`는 방패 표면 섬광에만 사용한다.
