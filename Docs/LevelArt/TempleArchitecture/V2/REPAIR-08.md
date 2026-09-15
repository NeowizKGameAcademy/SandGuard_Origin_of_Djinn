# Concept enclosure pass 08

적용 씬: Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity
컨셉 비교 항목 5번: 성벽, 문, 회랑.

## 변경

- 성벽 6구간의 연속 덮개를 개별 석재로 분할하고 일부 윗모서리에 작은 파손을 넣었다. 기존 덮개 범위 안에서 수정한다.
- 성벽에 하부 석재 줄과 상단 마감을 추가하고, 기존 벽 기둥에 받침과 머리 장식을 부착했다.
- 문기둥 12개에 얕은 석재 테두리와 층진 상단을 추가했다. 정문 2곳의 가로보에는 개별 덮개석과 하부 장식을 추가했다.
- 기존 회랑 4곳과 기둥 28개의 받침/머리, 세로 돌출선, 청록·청동 띠를 보강했다. 새로운 기둥은 추가하지 않았다.
- 기존 문 개구부, 플랫폼, 계단, 보행 충돌체, 조각상 및 부조는 유지했다. 정문 앞에 새 계단은 만들지 않았다.

## 검증

- 08-preservation-validation.json: 원본 C_ 충돌 메시, 참조 지형 및 조각상 157개 정점·면·월드 변환 변경 0.
- 기존 자체 부조 147개 그룹 유지. model-art 벽 장식 총 3개 및 조각상 각 1개 유지.
- 원본 플랫폼·계단 메시 참조 및 변환 보존 검사 통과.
- 원본 표면 1,356점 누락 0, 최대 높이 오차 약 0.00000763m.
- CharacterController.Move: 플랫폼 17개, 통로 22개 양방향, 벽 밀기 92곳 통과. 밀기 상승 0m, 반복 점프 최고 상승 1.546m, 실패 0.
- 08-player-portal.png, 08-player-portico.png, 08-player-enclosure.png 및 전경을 실제 Unity 촬영으로 검토했다.
- 전체 전투/스킬 조합 검증과 환경·재질·조명 작업(6번)은 별도다.

## 재생성

1. Blender --background --python Tools/Art/build_temple_architecture_v2.py
2. Unity -executeMethod TempleCourtyardBuilder.Build -courtyardStage 08-enclosure
3. Unity -executeMethod TempleRepairCaptures.Run