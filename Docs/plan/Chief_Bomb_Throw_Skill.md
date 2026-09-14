# Chief 폭탄 투척

보스 프리팹 `Assets/Enemy/Generated/Enemy_Chief.prefab`에 ChiefBombThrowSkill을 연결했습니다.

- 사용자가 제공한 `Docs/model-art/bandit-leader/*@Throw.fbx`를 `Chief_Throw.fbx`로 가져옵니다. 클립 길이는 2.2초입니다.
- Throw 클립의 38%(약 0.84초), 오른손을 머리 위에서 앞으로 뻗는 시점에 애니메이션 이벤트가 폭탄을 놓습니다. 이벤트 중복은 무시하며 이벤트가 누락되면 폭탄을 제거합니다.
- 황금 방패 소환을 우선하고, 소환 완료 후 폭탄 투척을 선택합니다. 투척 중 새 방패 소환/근접 공격/이동을 막습니다. 방패 유지 효과는 함께 사용할 수 있습니다.
- 시전 시작 때 대상 위치를 고정합니다. 기본 사거리 8m, 비행 시간 1초, 추가 도화선 0.35초, 피해 20, 폭발 반경 2.5m, 투척 동작 종료 후 쿨다운 6초입니다.
- 시전자 충돌체와의 충돌을 무시합니다. 적대 대상만 피해를 받고, 여러 충돌체는 한 번만 계산합니다. 폭발 피해 방향도 전달하므로 기존 정면 방어 규칙을 따릅니다.
- 투척 전 취소/사망은 손의 폭탄을 제거합니다. 이미 투척된 폭탄은 시전자 사망과 관계없이 폭발합니다. 비활성화 및 풀 재사용 시 시전 상태를 정리합니다.
- 투척 중 오른손 칼을 숨겼다가 복구합니다. 손에 붙일 때 폭탄의 월드 크기를 보존합니다.

튜닝은 보스의 ChiefBombThrowSkill 인스펙터에서 합니다. 애니메이션 이탈 시점은 Chief_Throw FBX의 ReleaseChiefBomb 이벤트입니다.

재연결 메뉴: SandGuard/Enemy/Connect Chief Bomb Throw Skill.

검증 결과: Logs/chief-bomb-tests.xml. 실제 PlayMode 투척 촬영: Docs/vfx-preview/ChiefBombSkill/Throw_Release.png.
