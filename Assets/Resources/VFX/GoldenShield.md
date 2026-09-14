# 소환형 황금 방패 VFX

프리팹: `Prefabs/VFX_Chief_Golden_Shield_Loop.prefab`

기존 `TowerShield.prefab`의 실제 형상을 사용하며 크기는 1.12배입니다. 금빛 반투명 표면과 빛나는 테두리로 구성됩니다. 기존 짧은 방어 피격 VFX와 별개의 지속형 효과입니다.

- 활성화하면 0.35초 동안 소환되어 유지됩니다.
- `VfxGoldenShield.PulseHit()`으로 피격 섬광을 재생합니다.
- `Dismiss()`로 0.3초 동안 해제합니다. 이후 비활성화/풀 반환은 호출자가 처리합니다.
- `Restart()` 또는 재활성화로 소환 상태를 초기화합니다.
- 중심 원점 기준이며 로컬 +Z가 방패 전면입니다.

이 프리팹은 시각 효과만 제공합니다. 충돌체, 피해 차단, 보스 스킬 발동 로직은 포함하지 않습니다.

재생성 메뉴: `DesertTower/VFX/Build Chief Summoned Golden Shield`

Unity 촬영 및 검증 결과: `Docs/vfx-preview/GoldenShield/`
