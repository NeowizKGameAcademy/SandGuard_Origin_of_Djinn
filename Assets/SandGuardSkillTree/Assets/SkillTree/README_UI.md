# 스킬 구매 · 장착 UI v2

확정한 금색 / 청록색 / 짙은 석판색 스타일을 사용하는 Canvas UI입니다. 기존 스킬 10개의 ID·비용·선행 관계·허용 슬롯을 유지합니다. 스킬 효과와 성장 시스템을 새로 구현하는 변경은 아닙니다.

## 가장 먼저 확인하기

Unity 상단 메뉴 **SandGuard → Skills → Create Styled Preview Scene**을 누르고 Play를 누르세요. 플레이어 없이 창이 열리고, 20 포인트로 구매와 장착을 시험할 수 있습니다. 기존 **Create Standalone Test Scene** 메뉴도 새 화면으로 연결됩니다.

미리보기에서 이동 탭의 대시를 선택하고 구매한 다음 하단 Shift 슬롯을 클릭하면 장착됩니다. 슬롯 우측 ×로 해제합니다. 한 스킬을 다른 슬롯으로 옮길 때는 기존 슬롯에서 먼저 해제합니다. 구매·장착은 실제 Service 데이터를 변경합니다.

## 게임 씬에 배치하기

1. 이 폴더 아래 **Generated/SkillTreeUI.prefab**을 씬에 한 번 넣습니다. 씬 전체에서 하나의 UI와 Session을 사용하세요.
2. 씬에 넣은 SkillTree UI를 선택하고 **SandGuard → Skills → Connect Selected UI To Scene Player**를 누릅니다. 기존 PlayerInputReader를 찾아 Player와 Suspend While Open에 연결하고, FacilityBuildMenu도 창이 열릴 동안 숨기도록 등록합니다. 다른 플레이어 구조라면 Inspector에서 직접 넣으세요.
3. **Generated/SkillAltarAnchor.prefab**을 스킬 창을 열 타워의 자식으로 넣습니다. 플레이어가 서게 될 바닥 높이에 위치시키고 Interaction Radius를 조절합니다. 기본 4 Unity 단위입니다. 위층까지 열리지 않게 3D 거리로 판정합니다.
4. 플레이어로 접근하면 **F 스킬 제단** 안내가 나옵니다. F를 누르면 창이 열리고, F / Esc / 오른쪽 위 ×로 닫힙니다. 범위를 벗어나거나 제단이 비활성화돼도 닫힙니다.

일반 게임 씬에서는 **Standalone Preview를 끄세요.** 이것을 켜면 제단 거리 검사 없이 시험용으로 열 수 있습니다. Player는 씬에 있는 실제 플레이어 Transform을 지정합니다. 비어 있으면 Player 태그 오브젝트를 시작할 때 한 번 찾습니다.

Suspend While Open에는 창이 열릴 동안 멈출 **입력, 카메라 조작, 건설 메뉴 컴포넌트**를 넣습니다. 닫을 때 각 컴포넌트의 원래 활성 상태와 커서 상태를 복원합니다. UI 자신, Session, Canvas는 넣지 마세요. 게임 시간을 일시정지하지는 않습니다.

F 입력을 별도 상호작용 관리자가 이미 처리한다면 Listen For Interaction Key를 끄고 그 관리자가 `Open()` / `Close()`를 호출하도록 연결합니다. 다른 상호작용 팝업의 우선순위는 그 관리자에서 조정하세요.

## 기존 검은색 시험 화면 교체

기존 씬의 **SkillTree Standalone Test** 오브젝트를 선택하고 **SandGuard → Skills → Upgrade Selected Test Panel**을 누릅니다. 기존 Session과 Definition을 유지하면서 TestPanel을 새 창으로 바꿉니다. 이후 Player와 제단을 위 순서대로 연결하세요. 씬 저장은 Unity에서 직접 합니다.

UI가 있는 오브젝트에서는 이전 OnGUI 테스트 화면이 표시되지 않습니다.

## 프리팹을 편집하고 싶을 때

함께 제공한 두 프리팹은 **기본 프리팹**으로, 실행 시 Canvas 자식들을 구성합니다. **SandGuard → Skills → Create Styled UI Prefabs**를 누르면 Canvas 계층까지 포함하는 편집용 프리팹을 만듭니다. 기본 프리팹이 이미 있으므로 `SkillTreeUI 1.prefab`처럼 새 이름으로 저장되며, 기존 프리팹을 덮어쓰지 않습니다.

편집용 프리팹의 Canvas에서 제목·패널·슬롯 위치를 조절할 수 있습니다. 스킬 노드와 선행 연결선은 선택한 계열 데이터에 따라 실행 중 생성됩니다. 닫기 버튼 경로 `Modal/Window/Close`는 유지하세요. 캔버스는 1920×1080을 기준으로 화면에 맞게 스케일됩니다.

## 데이터와 리소스

- **Generated/SkillTreeData.asset**: 기존 코드 기준 이동 3, 공격 4, 타워 3. 비용과 선행 관계는 Inspector에서 변경합니다. 레벨 조건은 없습니다.
- **Generated/SkillUITheme.asset**: 금색·청록색·잠김 색, 한글 폰트, 패널·링·장식 및 스킬 ID별 아이콘.
- **Art/**: 14개 PNG, 한글 폰트와 폰트 라이선스. 프레임 2개는 9-slice로 늘어납니다. HeaderScarab은 투명 배경 장식입니다.
- 스킬 설명은 데이터에 입력한 내용을 우선 표시합니다. 비어 있는 기존 데모 설명은 ID별 기본 설명으로 보완합니다.
- 여러 선행 조건은 모두 충족해야 하며, 다른 계열 선행 조건도 구매 판단과 상세 설명에 반영합니다. 노드가 많아지면 트리 영역을 드래그하거나 휠로 이동합니다.

## 연결 범위

Session의 `ISkillExecutor` 연결 방식은 유지합니다. **Executor Source가 비어 있으면 구매·장착만 시험하며 캐릭터 효과는 실행하지 않습니다.** 현재 PlayerSkillCaster의 고정 Q/E/R 입력, 이동기, PlayerProgression의 포인트 지갑은 이번 UI 변경에서 수정하지 않았습니다. 실제 게임 적용 시 입력을 Session의 UseQ/UseE/UseR/UseShift/UseSpace로 전달하는 기존 연결부와 실행기를 구성해야 합니다.

Space 슬롯은 추가 공중 점프용이며 기본 지상 점프를 대체하지 않습니다. 패시브·타워 해금은 슬롯을 차지하지 않고 기존 `IsLearned(id)`로 조회합니다. 기존 HUD와 포인트 지갑을 연결할 때 하나의 권한 있는 상태를 사용하고 중복 지급·이중 차감을 피하세요.

## 검증 상태

순수 C# 구매·장착 테스트 34개 및 Unity 6000.2.8f1 라이브러리 기준 Core / Runtime / Editor 컴파일을 검사했습니다. PNG 알파 채널, 프레임 크기, 프리팹·데이터·스크립트 GUID 참조도 정적 검사합니다.

별도 Unity 에디터는 라이선스 IPC 연결 단계에서 시작을 완료하지 못했습니다. **실제 Unity 가져오기, 프리팹 역직렬화, Play Mode 및 화면 렌더링은 미검증**입니다. 적용 후 위의 미리보기 메뉴로 먼저 확인하세요. 오류가 발생하면 Console의 첫 오류를 기준으로 확인할 수 있습니다.
