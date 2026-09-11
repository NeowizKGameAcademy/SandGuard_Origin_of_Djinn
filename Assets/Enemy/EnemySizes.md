# 주인공 대비 적 크기

게임용 `Generated/Enemy*.prefab`에 적용한 비율:

| 종류 | 주인공 대비 |
| --- | --- |
| Swordsman (검사) | 1 |
| Assassin (닌자) | 1 |
| ShieldGuard (방패병) | 1.5 |
| HammerBrute (망치병) | 1.5 |
| Chief (족장) | 2 |

`EnemyScaleBuilder`가 주인공 FBX와 적 외형의 월드 좌표 렌더러 높이를 기준으로 `EnemyVisuals.localScale`을 설정합니다. 포즈·장비가 달라 육안 실루엣에는 작은 차이가 있습니다. 장비와 공격 기준점은 외형의 자식이므로 함께 확대됩니다. 플레이어와 적의 루트 Transform 배율은 유지하며 CapsuleCollider·NavMeshAgent의 높이와 반경을 조정합니다. 체력·피해·이동 속도·공격 사거리는 변경하지 않습니다.

`Connect Combat Art`로 다시 제작해도 비율이 적용됩니다. 크기만 다시 적용하려면 `SandGuard > Enemy > Apply Player Relative Sizes`를 실행하세요.

비교용 씬은 `Generated/EnemyScalePreview.unity`입니다. 이 씬은 나란히 비교하기 위한 정지 전시이며 AI는 꺼져 있습니다. 기존 정적 ArtPreview 모델은 원본 제작 기준 크기를 유지합니다.
