# 시설 건설 (앵커 → 메뉴 → 번호 → 생성 연출)

건설 시간은 없습니다. 플레이어가 빈 받침대에 다가오면 건설 메뉴가 뜨고, 번호 키를 누르면 그 자리에 시설이 **"짠" 등장 연출**과 함께 완성됩니다.

## 등장 연출 — `VfxPopIn` (범용)

`Assets/Resources/VFX/Runtime/VfxPopIn.cs`. 특정 시설에 묶이지 않고 렌더러가 있는 아무 오브젝트에나 붙습니다. 시설 프리팹에 없으면 건설 서비스가 붙이고 기본 완료 이펙트를 넣어 줍니다.

| 단계 | 시간 | 내용 |
|---|---|---|
| 연막 | 0초 | `VFX_Build_Poof`(ImpactVfxBuilder 스펙, `DesertTower > VFX > Build Facility Pop-In Poof`) — 모래 구름 26개가 자리를 덮음. 크기는 대상 바운즈로 자동 조절 |
| 숨김 | 0~0.12초 | 대상 렌더러 꺼짐. 시설은 이미 자리에 있음 |
| 드러남 | 0.12초 | 렌더러 켜짐 + `VFX_Build_Complete`(플래시·링·금 큐브) + 카메라 셰이크 0.08 |
| 펀치·플래시 | 0.12~0.34초 | 스케일 1.25→1, 재질 `_BaseColor`를 흰색에서 원래 색으로(MaterialPropertyBlock, 재질 공유 안전) |

수치는 컴포넌트 인스펙터(`RevealDelay`, `PunchScale`, `FlashColor`, `Shake` 등)에서 조정합니다. 홀로그램 미리보기는 뺐습니다.

## 만들기·연결

- `SandGuard > Facility > Create Missing Assets` — `Assets/Facility/Generated/`에 `TowerAnchor.prefab`(받침대), `FireCobraTower.prefab`(화염 코브라), `FacilityCatalog.asset`, `FacilityBuildService.prefab`, `FacilityBuildMenu.prefab`을 만듭니다.
- `SandGuard > Facility > Wire Into Player And Enemy Scene` — `PlayerAndEnemyTest.unity`의 모든 `LevelBuildSlot` 아래에 받침대를 놓고 서비스·메뉴를 넣습니다. 허용 시설 목록이 빈 슬롯에는 `cobra`를 넣습니다. `SandGuard > Player And Enemy > Rebuild Test Scene`도 같은 연결을 합니다.

## 구성 (`Assets/Facility`)

| 파일 | 역할 |
|---|---|
| `Runtime/FacilityAnchor.cs` | 슬롯 위 받침대. 접근 반경, 메뉴 높이 |
| `Runtime/FacilityBuildService.cs` | `IFacilityBuilder` 구현. `BuildSlotRegistry` + `PlacementPhaseRule` + `SlotPlacementRule`로 검사 → 생성 → 점유 → `VfxPopIn.Play()` |
| `Runtime/FacilityBuildMenu.cs` | 근접 감지와 uGUI 메뉴. `PlayerInputReader.SlotSelected`(숫자 키 1~9)로 카탈로그 순서의 시설을 짓는다. 입력기가 없는 씬에서는 키보드 숫자 키를 직접 읽는다 |
| `Runtime/FacilityCatalog.cs` | 메뉴에 나오는 시설 목록(ID, 이름, 프리팹, 아이콘, 비용) |
| `Runtime/FacilityInstance.cs` | 지어진 시설의 ID·슬롯·표시 정보(`FacilityViewData`) |
| `Art/Tower/` | `TowerBase.obj`(= `Docs/model-art/Tower/Tower-1.obj`), `FireCobra.obj`(= `Tower-0.obj`), 단색 URP 재질 |
| `Art/UI/` | `Docs/ui/facillity-implementation-ui.png`에서 `Docs/ui/crop_facility_ui.py`(Blender)로 잘라 낸 번호·아이콘·원판 스프라이트 |

## 모델 이름과 크기

원본 이름과 역할이 반대입니다: **`Tower-1.obj`가 받침대**(4×1.8×4m, 바닥 원점), **`Tower-0.obj`가 코브라**(원점 y 1.3~4.7). 두 모델은 같은 좌표계라 받침대 원점에 코브라를 그대로 놓으면 맞물립니다. 받침대는 슬롯 `footprint.x / 4` 배율로 줄이고(슬롯 1.4m → 0.35배, 받침대 0.63m, 코브라 포함 약 1.65m), 시설은 받침대 자식이라 같은 배율을 따릅니다.

`.mtl`/팔레트 텍스처는 원본에 없어 받침대는 사암색, 코브라는 테라코타 단색입니다. MagicaVoxel 원본(.vox)이나 팔레트 PNG를 주시면 재질을 바꿀 수 있습니다.

## 조정 지점

- 접근 반경: `FacilityAnchor.interactionRadius` (기본 3.5m), 메뉴 높이 `menuHeight`
- 시설 추가: `FacilityCatalog.asset`에 항목 추가(ID는 슬롯의 `allowedFacilityIds`와 일치해야 함). 메뉴 번호는 목록 순서
- 단계 제한: `FacilityBuildService.SetPhase` — `PlacementPhaseRule`이 준비·전투 단계에서 슬롯 건설을 허용, 승리·패배·일시정지에서 거부
- 연출: `FireCobraTower.prefab`의 `VfxPopIn`(연막·드러남 프리팹, 지연, 펀치, 플래시, 셰이크). 서비스의 `buildCompleteVfx`는 `VfxPopIn`에 완료 이펙트가 없을 때의 기본값

## 아직 없는 것

- 마나 차감(`IManaWallet` 구현체 없음 — 비용은 표시값), 자유 배치, 시설 체력·수리·철거(`IFacility` 전체 구현), 적의 시설 공격 대상 등록
- 카탈로그에는 화염 코브라만 있습니다. UI 시트의 나머지 아이콘 4종(터렛·활·마법·수정)은 스프라이트만 준비됨

## 검증

`Assets/Facility/Tests/FacilityPlayModeTests.cs` — 멀리서는 메뉴가 닫혀 있고, 3.5m 안으로 오면 열리며, `Select(0)`으로 코브라가 받침대 자식으로 생성되어 연막이 먼저 뜨고 시설은 숨겨졌다가 지연 뒤 드러나며(`VFX_Build_Complete` + 펀치) 원래 크기로 돌아오고, 레지스트리에 점유가 기록되며, 같은 슬롯 재건설은 `Occupied`, 허용되지 않은 시설은 `FacilityNotAllowed`로 거부됨을 확인합니다. 렌더는 `Docs/model-art/facility-v1/captures/` (`menu-open / poof / reveal / complete`).
