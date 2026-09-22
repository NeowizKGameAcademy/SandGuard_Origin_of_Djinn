# 코브라 타워 파괴 VFX

프리팹: `Prefabs/VFX_Cobra_Destruction.prefab`

`Assets/2.Model/Prefabs/Tower_Cobra.prefab`의 TowerHealth 본체에서 메시를 분할하며 원본 UV·머티리얼을 유지한다. 받침대와 사거리 표시 원은 파편에서 제외한다.

- 0.04 / 0.21 / 0.38 / 0.55초: 푸른 부분 폭발 4회.
- 0.72초: 큰 푸른 섬광·충격파와 함께 본체 파괴.
- 파편은 회전하며 바닥에 내려앉고 4초에 VFX가 풀로 반환됨.

실제 건설 카탈로그의 코브라 프리팹에 `VfxDestructionOnDeath`를 연결했다. 연출은 원본의 자식이 아니므로 타워가 비활성화되거나 제거되어도 계속된다. 원본 렌더러·조명·파티클을 숨기며 자동 복구하지 않는다. 전시/리스폰에서만 `RestoreTarget()`을 사용한다.

기존 파괴·잔해 효과음은 각각 0.72초, 1.45초에 맞췄다. 파편은 정해진 궤적을 사용하는 시각 연출이며 실제 지형과 충돌하지 않는다.

전체 재생성·연결·미리보기: **DesertTower > VFX > Build Blue Cobra and Level Core + Preview**.

`Docs/vfx-preview/CobraDestruction/`에 실제 Unity 렌더 100프레임과 검증 결과가 있다. `Cobra_Destruction.gif`는 이 프레임을 합친 미리보기다.