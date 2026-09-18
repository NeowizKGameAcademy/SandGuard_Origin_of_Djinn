# 미니언 비주얼 에셋

`SkeletonMinion/SkeletonMinion_Visual.prefab`와 `AnubisMinion/AnubisMinion_Visual.prefab`를 씬에 드래그해 사용합니다.

- 원본: `Docs/model-art/skeleton-minion`, `Docs/model-art/anubis-minion`. 원본 파일은 수정하지 않았습니다.
- 각 프리팹은 Transform, SkinnedMeshRenderer 및 뼈대만 포함합니다. Animator, 전투/이동 스크립트, Collider, Rigidbody, NavMeshAgent, 무기는 붙이지 않았습니다.
- URP Lit 재질에 원본 Base Color 텍스처를 연결했습니다. 원본 색감에 이미 명암이 포함되어 있어 금속/발광 맵은 임의로 만들지 않았습니다.
- 크기는 원본 FBX 단위를 유지하며 루트 피벗을 발밑 중심으로 맞췄습니다. 원하는 게임 내 크기는 프리팹 인스턴스 루트 Scale로 조정하세요.
- `Models/*_UnriggedSource.fbx`는 원본 정적 모델입니다. 비주얼 프리팹은 Idle 파일에 포함된 리깅된 메시를 사용하며, 스킨과 본을 보존합니다.
- `Animations`에는 제공된 동작 FBX를 모두 보관했습니다. Generic Rig로 가져왔고 Idle/Walk 클립만 반복하도록 설정했습니다. 컨트롤러 및 자동 재생은 구성하지 않았습니다.

## 나중에 동작 추가하기

Animator는 프리팹 루트의 **모델 자식 오브젝트**에 추가하세요. 애니메이션 경로는 이 자식을 기준으로 검증했습니다. 해당 `Models/SkeletonMinion.fbx` 또는 `Models/AnubisMinion.fbx`에 포함된 Avatar를 연결하고 Controller를 지정합니다. 게임 로직은 바깥쪽 `_Visual` 루트 또는 이를 감싸는 게임플레이 프리팹에 붙일 수 있습니다.

Generic 리그이므로 다른 캐릭터에 대한 Humanoid 리타게팅은 설정하지 않았습니다. 무기를 추가할 때는 기존 손 본 아래에 배치하고 그립 위치를 맞추세요.

재생성 메뉴: `Tools > Minion Visuals > Build Visual Prefabs`. 재실행하면 생성된 재질과 비주얼 프리팹을 다시 만드므로, 후속 게임플레이 작업은 별도 프리팹 또는 Variant에서 진행하는 편이 좋습니다.

검증 기록: `Docs/model-art/minion-visuals/unity-validation.txt`. 소스 미리보기 PNG는 Blender 렌더입니다.
