# Concept architecture pass 06

적용 씬: Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity
참조: ../Concepts/Courtyard-05-concept-v1.png

## 적용

컨셉 비교 목록 2번(탑과 중앙 신전)의 건축 디테일을 반영했다.

- 상부 탑 9개: 개별 덮개석으로 나눈 처마, 하부 돌출 장식, 청록색 띠, 기둥 머리와 받침, 기둥의 세로 돌출선, 하부 기단 및 벽에 붙은 세로 기둥 장식.
- 중앙 신전 4개 단: 층별 처마 및 기단 석재, 얕은 석재 벽 틀, 청록·청동색 가로 장식.
- 새 장식은 기존 벽과 기둥에 붙는 D_Architecture 메시다. 별도의 떠 있는 벽판은 생성하지 않는다.
- 원본 플랫폼의 지붕 보행면 위로 새 구조를 올리지 않는다. 새 장식은 원본 이동 공간에 대한 검사와 Boolean 절단을 적용한다.
- 부조·문양 전면 개편(3번), 성벽·문·회랑(5번), 재질·환경·조명(6번)은 후속 단계다. 조각상과 원본 보행 경로는 유지한다.

## 검증

- 06-geometry-preservation.json: 수정 직전 Blender와 현재 Blender의 C_ 충돌 메시, 원본 REFERENCE 메시, 조각상 157개에 대해 정점·면·월드 변환 비교. 변경 0.
- Unity 생성 시 원본 플랫폼·계단 메시 참조와 변환 보존 검사 통과.
- collision-repair-validation.txt: 원본 표면 1,356점 누락 0, 최대 높이 오차 0.00000763m.
- traversal-verification.txt: 플랫폼 17개, 통로 22개 양방향, 벽 밀기 92곳 모두 통과. 벽 밀기 상승 0, 반복 점프 최고 상승 1.546m, 실패 0.
- 06-player-tower-facade.png, 06-player-sanctuary.png, 06-player-tower-interior.png 및 전경 촬영을 검토했다.
- 이 검증은 전체 전투·스킬 조합의 플레이 테스트를 대체하지 않는다.

## 재생성

1. Blender --background --python Tools/Art/build_temple_architecture_v2.py
2. Unity -executeMethod TempleCourtyardBuilder.Build -courtyardStage 06-architecture
3. Unity -executeMethod TempleRepairCaptures.Run