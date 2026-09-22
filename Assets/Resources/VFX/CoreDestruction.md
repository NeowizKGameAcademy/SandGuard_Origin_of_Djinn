# 코어 파괴 VFX

프리팹: `Prefabs/VFX_Core_Destruction.prefab`

`Level.unity`가 사용하는 `Assets/2.Model/Prefabs/New Core.prefab` 기준이다. 중앙 결정, 떠 있는 조각 3개, 회전 링 3개의 원본 메시·UV·머티리얼을 유지하고 28개 파편을 만든다. 사거리 표시 원은 파편에서 제외한다.

- 0.04 / 0.21 / 0.38 / 0.55초: 서로 다른 지점에서 푸른 부분 폭발.
- 0.72초: 큰 푸른 폭발과 함께 모델이 부서짐.
- 이후 파편이 회전하며 낙하하고 2.8~3.8초에 작아지며 사라짐. 풀 반환은 4초.

`VfxCoreDestruction.BindTarget`은 파괴 순간의 조각·링 애니메이션 위치와 회전을 복사하고 원본 렌더러·조명을 숨긴다. 원본 게임 로직은 유지한다. 원본 자동 복구는 없으며 전시/리스폰에서만 `RestoreTarget()`을 사용한다. 파편은 정해진 궤적을 사용하는 시각 연출이며 실제 지형과 충돌하지 않는다.

`New Core.prefab`에는 `VfxDestructionOnDeath`가 연결돼 있고, `Level.unity`의 `CoreReceiver.onDefeated`가 `Fire`를 호출한다. 오버레이는 기존 설정대로 판정 1초 후 표시된다.

전체 재생성·연결·미리보기: **DesertTower > VFX > Build Blue Cobra and Level Core + Preview**.

`Docs/vfx-preview/CoreDestruction/`에 실제 Unity 렌더 100프레임과 검증 결과가 있다. `Core_Destruction.gif`는 이 프레임을 합친 미리보기다.