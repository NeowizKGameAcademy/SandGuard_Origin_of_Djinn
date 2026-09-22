# 아누비스·관 타워 파괴

- `Prefabs/VFX_Anubis_Destruction.prefab`
- `Prefabs/VFX_Coffin_Destruction.prefab`

실제 건설 카탈로그의 `Tower_Anubis.prefab`, `Tower_Coffin.prefab`에 연결한다. 각 TowerHealth 본체의 원본 메시·UV·머티리얼을 유지한 파편이며 사거리 표시와 별도 받침대는 제외한다.

0.04 / 0.21 / 0.38 / 0.55초에 푸른 부분 폭발, 0.72초에 큰 폭발과 본체 파괴가 발생한다. 파편은 바닥으로 퍼져 떨어지고 2.8~3.8초에 작아지며 사라진다. VFX 풀 반환은 4초다. 파편 궤적은 시각 연출이며 실제 지형과 물리 충돌하지 않는다.

`VfxDestructionOnDeath`가 사망 이벤트를 구독하고 원본과 분리된 VFX를 생성한다. 기존 `VfxObeliskDestruction`의 공통 석재 파괴 동작을 재사용한다. 타워가 제거되어도 연출은 계속되며 풀 반환 시 원본을 되살리지 않는다.

재생성·연결·미리보기: **DesertTower > VFX > Build Anubis and Coffin Destruction + Preview**.

Unity 렌더 결과는 `Docs/vfx-preview/AnubisDestruction`, `CoffinDestruction`에 각각 100프레임과 `validation.txt`로 저장된다.

PlayMode: `DesertTower.VFX.Tests.SummonerTowerDestructionTests` — 두 실제 프리팹의 치명타, 위치/스케일, 원본 숨김, 원본 제거 후 재생, 파편 소멸, 풀 재사용을 검증한다.
