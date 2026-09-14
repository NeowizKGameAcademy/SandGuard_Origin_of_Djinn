# 폭탄의 타워 정지 요청 — 타워 담당자 연결용

공용 Combat 어셈블리의 `CombatEffectSignals.TowerDisableRequested` 이벤트를 구독합니다. `TowerDisableRequest.TargetEntityId`가 타워의 `ICombatTarget.EntityId`와 일치할 때 처리하면 됩니다. 구독 해제도 수신자가 관리합니다.

요청 데이터:
- `Duration`: 초 단위 정지 요청 시간. 보스의 `ChiefBombThrowSkill.towerDisableDuration`으로 설정하며 기본값 5초, 0이면 요청 없음.
- `Cause`: 기존 DamageInfo. 시전자 ID, 진영, `chief.bomb` 원인 ID, 피격 위치와 방향 포함.
- `TargetEntityId`: 정지 대상 타워 ID.

폭발 피해가 적용되고 살아 있는 적대 `CombatTargetKind.Tower`에만 요청합니다. 한 폭발에서 같은 대상의 여러 충돌체는 한 번만 처리합니다. 다른 종류 대상, 아군, 피해 거부, 파괴된 타워는 제외합니다. 수신자가 없어도 일반 폭발 피해는 정상 동작합니다.

실제 타워 정지/재개, 중첩 또는 지속시간 갱신 규칙, VFX 생성/해제는 구현하지 않았습니다. 타워 코드는 수정하지 않았습니다.

기존 연출: `Assets/Resources/VFX/Prefabs/VFX_Facility_Disabled_Loop.prefab`. 필요하면 그 안의 `VfxDisabledVisual.BindTarget`으로 외형을 어둡게 할 수 있으며, 종료 시 원상복구 및 오브젝트 수명 관리는 타워 담당자가 연결합니다.
