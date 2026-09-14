# 계단·연결길 난간 분리 및 UV 정리 v2

## 결과

실제 V6 원본의 `RAILS_REBUILT_*` 메시 22개를 전수 검사했다. 계단 18구간, 평평한 연결길 4구간 모두 한 메시 안에 좌우 두 개의 분리된 덩어리가 있었다. 이를 **22개 구간 부모 + 44개 독립 난간**으로 정리했다. 외곽 성벽의 `Outer_wall_stone`과 `Wall_coping`은 길 양쪽 난간이 아니므로 이번 분리 대상에서 제외했다.

Unity에서는 각 구간 이름 아래 `Left` / `Right` 자식이 있으며, 부모에는 렌더러와 메시가 없다. 각 자식의 피벗은 해당 난간 중심에 있어 한쪽만 선택·이동·교체할 수 있다. Blender에서는 `구간명__Left` / `구간명__Right`로 이름 붙였고 로컬 X축을 난간 길이에 맞췄다. 경사 구간은 올라가는 방향, 수평 구간은 일관된 양의 수평 축 방향을 기준으로 좌우를 정했다.

## 파일

- Unity 프리팹: `Assets/DesertTowerLevels/TrimSheet/Prefabs/DesertTemple_TrimRails_v2.prefab`
- Unity 비교 씬: `Assets/DesertTowerLevels/TrimSheet/Scenes/SandGuard_Rails_Comparison_v2.unity`
- Unity 메뉴: `SandGuard > Level Art > Open Separated Rails v2`
- Blender 원본: `SandGuard_Rails_Split_v2.blend` (난간 44개와 구간 부모 22개, 텍스처 내장)
- FBX: `Assets/DesertTowerLevels/TrimSheet/Models/DesertTemple_Rails_Split_v2.fbx`
- 난간 근접: `RailsV2_After_Stairs.png`
- 평평한 길 근접: `RailsV2_After_Road.png`
- 전체: `RailsV2_After_Overview.png`

동일 구도의 `RailsV2_Before_*.png`는 이전 v1 프리팹이다. 그 프리팹은 일부 난간 3구간에만 트림을 적용한 상태이므로, 전후 비교에는 분리·UV 수정과 함께 나머지 19구간의 새 재질 적용도 포함된다. 비교 씬에서 BEFORE/AFTER를 한 번에 하나만 활성화한다.

## UV 변경

같은 사암 트림 시트와 같은 재질 하나를 공유한다. 이전의 면마다 비율을 맞추던 방식 대신, 난간의 길이와 단면 둘레를 실제 거리로 펼쳤다. 시트 가로 한 장을 8m로 통일해 긴 연결길과 짧은 진입 계단에서 무늬 크기가 같도록 했다.

윗면·모따기·좌우 측면을 사암 띠 하나 안에서 이어 펼쳤고, UV 절개선은 밑면과 끝면에만 뒀다. 끝면의 청록색 조각은 사암 마감으로 바꿨다. 끝면과 밑면은 별도 UV 섬이므로 그 경계까지 모든 무늬가 이어진다고 보장하지 않는다. 원본의 1254px 이미지와 Unity의 2048px 확대 임포트 설정은 동일하다.

## 검증 및 재생성

- `rails-v2-validation.json`: 전체 구간 목록, 원본 덩어리 수, 분리 후 이름, 좌표 보존 오차, 윗면/측면 공유 정점의 UV 연속성 검사.
- `rails-v2-unity-validation.txt`: 원본과 난간 범위 비교, 저장한 22개 부모/44개 자식 재검사, 재질 공유 및 원본 충돌 메시·변환 유지 검사.
- `build_rails_v2.py`: 원본 V6 FBX에서 난간을 다시 분리하고 UV를 작성한다.
- Unity 메뉴 `SandGuard > Level Art > Build Separated Rails v2`: v2 프리팹·씬·전후 이미지를 다시 생성한다.

v1 시험 프리팹과 게임플레이 씬을 덮어쓰지 않는다. v2 프리팹에는 v1의 계단·플랫폼 시험 적용도 유지된다. 이번 변경은 아트 구조와 UV에 한정했으며, 이동·전투 플레이 테스트는 별도로 수행하지 않았다.
