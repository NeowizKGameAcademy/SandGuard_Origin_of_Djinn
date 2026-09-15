# Courtyard visual repair (05)

적용 씬: Assets/TempleArt/ArchitectureV2/Level_TempleCourtyard.unity

## 수정 내용

- 계단 18구간에 시각용 석재 채움 메시를 추가했다. 디딤판 아래의 열린 부분을 막으며 원본 경사 콜라이더는 변경하지 않는다. V_ClosedRisers 메시에는 콜라이더가 없다.
- 탑 내부에서 여전히 떠 있던 장식 패널을 제거했다. 04 단계의 제거가 불완전했던 부분을 같은 촬영 위치에서 재확인했다.
- model-art에서 가져온 벽 장식은 맵 전체에 총 3개: wall-deco, wall-deco-anubis, god-la 각 1개다.
- 나머지 벽 문양은 Blender 메시로 직접 제작한 연꽃, 날개 달린 태양, 풍뎅이, 눈, 파피루스, 별자리, 기하무늬, 물결의 8종이다. 별도 판 없이 벽면에 붙으며 89곳에 분산했다.
- model-art의 la-dragon, osiris-dragon, obelisk-giant 조각상을 각각 1개씩 배치했다. 원본 UV와 텍스처를 유지하고 개별 받침대 및 충돌 메시를 추가했다.
- Blender/FBX 좌표 변환 후 Unity 실제 조각상 중심은 라 (34,4.28,-43), 오시리스 (-34,4.28,-43), 오벨리스크 거인 (42,5.63,42)이다.

## 검증

- Unity 씬 생성 완료. 조각상 3개 및 닫힌 계단 18구간 검사를 통과했다.
- 원본 플랫폼과 계단의 메시 참조 및 변환 보존 검사를 통과했다. Assets/1.Scene/Level.unity는 변경하지 않았다.
- 원본 보행면 1,356점: 누락 0, 최대 높이 오차 0.00000763m.
- 실제 CharacterController.Move 검사: 플랫폼 17개, 통로 22개 양방향, 벽 밀기 92곳 모두 통과. 벽 밀기 상승 0m, 반복 점프 최고 상승 1.546m, 실패 0.
- 자동 이동 검사는 전체 전투 및 스킬 조합의 플레이 테스트를 대체하지 않는다.
- 05-player-entry.png 및 05-player-tower-interior.png를 사용자 이미지와 같은 위치에서 촬영해 틈 및 떠 있는 패널이 제거됐음을 확인했다.
- 05-player-wall-reliefs.png와 05-player-statue-*.png는 실제 Unity의 새 문양 및 조각상 촬영이다.

## 재생성

1. Blender 백그라운드에서 Tools/Art/build_temple_architecture_v2.py 실행.
2. Unity에서 TempleCourtyardBuilder.Build -courtyardStage 05-art-refinement 실행.
3. Unity에서 TempleRepairCaptures.Run 실행. 이동 검사와 근접 촬영을 함께 수행한다.

편집 가능한 Blender 원본: Temple_Courtyard_v2.blend
생성 개수 기록: model-report.json
충돌 및 이동 기록: collision-repair-validation.txt, traversal-verification.txt