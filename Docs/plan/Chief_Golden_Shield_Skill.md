# Chief 황금 방패 스킬

`Assets/Enemy/Generated/Enemy_Chief.prefab`에 연결되어 있습니다. 기존 보스 프리팹 참조를 사용하는 스폰에도 적용됩니다.

EnemyBrain이 교전 대상을 선택하면 근접 공격보다 먼저 `ChiefGoldenShieldSkill.TryUse(target)`을 시도합니다. 이미 휘두르는 공격은 끝낸 뒤 발동합니다. 소환 0.35초 동안 이동과 새 공격을 멈추며, 이때는 아직 방어하지 않습니다. 소환 완료 후 4초간 방어하며 이동과 근접 공격을 재개합니다. 해제 0.3초부터 방어 판정은 사라지고, 해제 완료 후 8초의 재사용 대기시간을 갖습니다.

방어 규칙은 방패병과 같은 EnemyShield입니다. 정면 반각 60도(총 120도)는 완전 차단하고 측면, 후방, 방향 없는 피해는 통과합니다. 실제 차단 시 황금 방패가 반짝입니다.

사망, 비활성화, AI 중지, 낙하 시 방어와 VFX를 정리합니다. 재활성화 및 EnemyBrain.ResetForReuse에서 상태를 초기화합니다. VFX는 보스 자식 하나를 재사용합니다.

보스의 ChiefGoldenShieldSkill 인스펙터에서 소환/유지/쿨다운/사거리/로컬 위치를 조정할 수 있습니다. 전용 시전 애니메이션은 아직 없으며 소환 VFX로 예고합니다. 폭탄 스킬은 이번 연결 범위에 포함하지 않습니다.

재연결: `SandGuard/Enemy/Connect Chief Golden Shield Skill`. 전투 외형 재생성 시에도 연결을 보존합니다.

PlayMode 검증: `ChiefGoldenShieldTests`, `EnemyShieldTests`. 결과는 `Logs/chief-shield-tests.xml`입니다.
