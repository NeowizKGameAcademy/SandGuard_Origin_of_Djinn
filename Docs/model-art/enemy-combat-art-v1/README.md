# 적 전투용 외형 연결 결과 v1

2026-09-09 · Unity 메뉴 `SandGuard > Enemy > Connect Combat Art` (`Assets/Enemy/Editor/EnemyCombatArtBuilder.cs`) 실행 결과.

- `build-report.txt`: 적별 키·이동 클립 보정 배율·공격/사망 길이·본 수, 장비별 부착 본과 손 축 측정값.
- `captures/<적>_idle|move|attack|death.png`: PlayMode 테스트(`EnemyArtTests`)가 실제 Enemy 프리팹을 돌리며 찍은 렌더.
- `captures/<적>_idle_axes.png`: 손 축 표시. 빨강=손가락, 초록=엄지, 파랑=손바닥 법선, 노랑=팔뚝. 장비 그립 규칙을 정할 때 쓴 근거.
- `captures/<적>_bindpose_front|top.png`: 애니메이터를 끈 Mixamo 바인드 포즈(A포즈)에서의 장비 배치.

검증: `SandGuard.Enemy.Tests` PlayMode 12개 통과 (신규 6 + 기존 6). Humanoid 아바타, 2K 텍스처, URP 재질, 키, 장비 부착 본, 이동 시 힙 드리프트 0.45m 미만(루트 모션 누출 없음), Attack/Die 상태 전환, removeDelay 뒤 제거를 확인.

상세 사용법과 조정 지점은 `Assets/Enemy/Art/Characters/README.md`.

## 손·장비 보정 — 2026-09-09

`SandGuard > Enemy > Fit Hands and Equipment`로 전투 외형 5종과 Enemy 프리팹 6개를 갱신했다. 손잡이를 손목에서 손바닥으로 옮기고, 곡도·망치의 축을 손잡이 기준으로 수정했다. 방패는 왼팔 보정으로 세우며 팔꿈치를 뒤로 접는다. 망치는 보조 손을 위쪽 손잡이에 맞추고 필요한 구간에는 주 손도 함께 보정해 팔 길이를 유지한다. 장비를 쥐는 손가락 자세를 유지하며 사망 시 보정을 해제한다.

최종 검증은 `Logs/enemy-equipment-fit-tests-v4.xml`, Enemy PlayMode 12개 통과. 이동·공격·복귀 전체 프레임의 망치 보조 손 거리와 방패 방향을 추가로 관측했다. 확대 렌더는 `captures/*_idle_hands.png`, `*_attack_hands.png`, 방패 뒤쪽은 `*_shield_back.png`이다. 실제 씬은 `Assets/Enemy/Generated/EnemyTest.unity`를 사용한다. 정적 `EnemyArtPreview`의 원본 모델 전시는 변경하지 않았다.
