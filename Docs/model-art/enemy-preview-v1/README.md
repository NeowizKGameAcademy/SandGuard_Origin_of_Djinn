# 적 외형 확인 결과

Unity 씬: `Assets/Enemy/Generated/EnemyArtPreview.unity`.

Play 후 상단에서 캐릭터와 시점을 선택하세요. 드래그 회전, 휠 확대/축소, 장비 숨기기, 망토 흔들림 토글을 제공합니다.

- `Lineup_Front.png`, `Lineup_Side.png`, `Lineup_Rear.png`: 5종 비교
- `종류_Front.png`: 개별 정면
- `종류_GameDistance.png`: 실제 Player 프리팹 카메라 설정으로 본 모습
- `Chief_Rear.png`: 망토와 등쪽 배치
- `validation.txt`: 높이, 바닥, 장비 수 검사
- `viewer-validation.txt`: 시점, 개별 선택, 장비 숨김 검사

모든 PNG는 Unity에서 실제 모델과 재질을 렌더링한 결과입니다. 몸체는 아직 정적 A포즈이고 손가락의 쥐는 자세와 전투 동작은 연결하지 않았습니다. 상세 연결 설명은 `Assets/Enemy/Art/Characters/README.md`에 있습니다.
