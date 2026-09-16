# Courtyard collision and art repair (04)

적용 씬: Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity

## 수정

- Assets/1.Scene/Level.unity에서 실제 충돌 메시의 월드 좌표 면을 읽었다. 원본 씬, 플랫폼 및 계단 시각 메시의 위치와 크기는 변경하지 않았다.
- 플랫폼 17개, 계단 경사 18개, 수평 연결길 4개를 원본 충돌에서 추출했다. 계단의 개별 디딤판은 시각용으로 유지하고 충돌에는 연속 경사면을 사용한다.
- 대각선 계단과 플랫폼 접합부에는 원본에 실제로 존재하는 면만 사용한다. 분리된 플랫폼에 임의의 옆면/끝면을 만들면 계단 입구에서 캐릭터를 막으므로 그런 면을 추가하지 않는다.
- 제거된 피라미드 전체 충돌체는 비활성 상태다. 새 건축물은 Blender의 모따기 전 구조 메시(C_*)에 충돌체를 둔다. 보이는 메시(S_*)와 부조·띠 장식에는 충돌체가 없다.
- 탑의 내실을 지면까지 연장하고 구조 기둥의 하부 지지를 연결했다. 탑 하부와 문탑의 충돌 벽은 수직으로 정리했다. 통로를 위해 잘라낸 구간에는 장식도 동일하게 잘라 떠 있는 패널을 없앴다.
- 기존에 제작된 난간 v2의 22쌍/44개 독립 메시 및 UV를 적용했다. 각 구간 아래 Left, Right를 따로 선택할 수 있다. ArchitectureV2/Rails에 좌우 재질 및 텍스처를 별도로 두었으며 같은 초기 사암 패턴에서 각각 편집할 수 있다.
- Docs/model-art의 wall-deco.obj, wall-deco-anubis.obj, god-la.obj의 UV와 각 텍스처를 보존하여 기존 문양과 섞었다.
- 기존 플레이어 및 3인칭 카메라를 유지한다.

## 검증

- collision-repair-validation.txt: 원본 보행면 1,356개 검사점, 누락 0, 최대 높이 오차 약 0.000008m.
- source-traversal-baseline.txt: 원본 씬의 CharacterController 이동 기준 검사.
- traversal-verification.txt: 동일한 크기의 CharacterController로 플랫폼 17개를 가로지르고 통로 22개를 양방향으로 이동. 모두 통과. 수직 벽 92곳을 밀었을 때 최대 상승 0m. 벽을 향한 반복 점프는 최고 상승 약 1.546m로 일반 점프 범위에 머물며 벽을 타고 누적 상승하지 않았다.
- 이동 검사는 건축 충돌을 검사하기 위해 기존 코어와 건설 슬롯의 점유 충돌을 검사 중에만 끈다. 저장된 씬에서는 유지된다. 바닥에 묻힌 진입 계단은 실제 지면 높이에서 출발한다.
- 이 자동 검사는 에디터의 실제 CharacterController.Move를 사용한다. 모든 전투, 스킬/대시 조합 및 적 내비게이션 검증을 대체하지 않는다.
- 04-collision-repair-*.png: 실제 Unity 전경. 04-player-*.png: 입구, 하부 플랫폼, 난간, 탑 내부, 벽 장식의 근접 촬영.

## 재생성

1. Unity에서 TempleCollisionAudit.Run 실행. 원본과 현재 씬을 읽어 Library/TempleCollisionAudit에 조사 데이터를 만든다.
2. Blender 백그라운드에서 Tools/Art/extract_temple_routes.py 실행. 원본 보행 충돌 면과 검사점을 route-collision-reference.json에 기록한다.
3. Blender에서 Tools/Art/build_temple_architecture_v2.py 실행. 편집 가능한 .blend, FBX, 모따기 전 충돌용 메시와 텍스처가 있는 장식을 생성한다.
4. Unity에서 TempleCourtyardBuilder.Build -courtyardStage 04-collision-repair 실행. 전용 씬, 프리팹과 촬영을 갱신한다.
5. Unity에서 TempleTraversalVerification.Run 실행. -templeVerifySource를 추가하면 원본 씬으로 같은 검사를 수행한다.
6. Unity에서 TempleRepairCaptures.Run 실행하면 근접 촬영을 갱신한다.

이전 validation-03-final.txt와 03-final-*.png는 수정 전 기록이다. 현재 충돌 판단에는 04 검증 기록을 사용한다.

