# 방패 가드 VFX — 일반 / Chief 황금

- 일반: `Prefabs/VFX_Shield_Front_Guard.prefab` (기존 GUID 유지, 크기 확대)
- 황금: `Prefabs/VFX_Shield_Gold_Guard.prefab`

`Assets/Enemy/Art/Equipment/TowerShield.prefab`의 실제 메시 범위를 측정하여 가로·세로 외곽 범위를 각각 12% 크게 맞춘다. 두 VFX의 크기, 육각 윤곽, 입자 수, 속도, 재생 시간은 동일하며 황금 버전만 윤곽·섬광·파편 색상이 다르다. 타격 순간의 0.25초 윤곽 섬광이며 지속 방어막 루프는 아니다.

방패 앞면 중앙에 배치하고 로컬 +Z를 방패 바깥쪽으로 향하게 한다. 프리팹 루트의 저장된 scale이 크기 보정값이므로 생성 후 무조건 `(1,1,1)`로 덮어쓰지 않는다. 다른 크기의 방패에 사용할 때는 이 scale에 추가 배율을 곱한다. 방패 뼈 아래 배치할 때 부모의 scale도 영향을 준다.

재생성: `DesertTower > VFX > Build Shield Guards (Standard + Chief Gold)`. 기존 Requested Set 전체 재생성에도 두 버전이 포함된다. 기존 8종 쇼케이스 목록은 그대로 유지한다.

검증 및 실제 방패 비교 렌더: `DesertTower > VFX > Preview Shield Guards` → `Docs/vfx-preview/ShieldGuards/`. Chief의 게임플레이 발동 로직은 이 작업에서 추가하지 않는다.
