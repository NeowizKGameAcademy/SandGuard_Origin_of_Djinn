# Concept relief pass 07

적용 씬: Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity
참조: ../Concepts/Courtyard-05-concept-v1.png

## 변경

- 기존 8종 선 문양을 닫힌 입체 메시로 확장했다. 연꽃 꽃잎, 태양 원반, 날개 깃, 풍뎅이 몸통, 눈, 파피루스, 별, 기하무늬와 물결에 면과 깊이를 추가했다.
- 석재 바탕, 청록색 면, 청동색 테두리를 조합했다. 테두리와 채움 면의 깊이를 달리해 겹침을 피했다.
- 26m 이상 탑 7개와 최고 탑 내벽, 중앙 신전의 선택된 벽면에 대형 문양 58곳을 추가했다. 탑별 대표 문양을 지정하고 중앙 신전은 일부 벽면을 비워 두었다.
- 기존 외벽/문기둥 문양을 포함해 살아 있는 자체 제작 문양 그룹은 147개다. 모델의 재질별 메시 개수와는 다른 수치다.
- model-art 벽 장식은 기존 3개, 조각상 3종은 각 1개 그대로 유지한다.
- 탑 벽의 실제 다각형 중심과 축소율로 부착 좌표를 계산한다. 독립된 판은 생성하지 않으며 이동 공간에 걸리는 문양은 검사/절단한다.
- 높이 비교에 허용 오차를 두어 26m 플랫폼의 소수점 오차로 일부 탑이 제외되는 문제를 수정했다.

## 검증

- 07-relief-validation.json: 충돌/참조 지형/조각상 157개 정점·면·월드 변환 변경 0.
- 부조 정점 59,592개에서 구조 메시까지 거리를 검사했다. 최대 거리 0.3131m, 허용치 0.37m를 넘는 정점 0. 이는 벽 근접 검사이며 면 전체의 정확한 접착을 수학적으로 보장하는 검사는 아니다.
- Unity 원본 플랫폼·계단 참조 및 변환 보존 검사 통과.
- 원본 표면 1,356점 누락 0, 최대 높이 오차 약 0.00000763m.
- CharacterController.Move: 플랫폼 17개, 통로 22개 양방향, 벽 밀기 92곳 모두 통과. 벽 밀기 상승 0m, 반복 점프 최고 상승 1.546m, 실패 0.
- 최종 탑 외관·입구·문기둥·내부·전경을 실제 Unity 촬영으로 확인했다. 전체 전투/스킬 조합 검증은 별도다.

## 재생성

1. Blender --background --python Tools/Art/build_temple_architecture_v2.py
2. Unity -executeMethod TempleCourtyardBuilder.Build -courtyardStage 07-reliefs
3. Unity -executeMethod TempleRepairCaptures.Run

주요 촬영: 07-player-tower-facade.png, 07-player-entry.png, 07-player-wall-reliefs.png, 07-reliefs-overview.png.
성벽/문/회랑 건축(5번)과 재질/환경/조명 전면 조정(6번)은 후속 작업이다.