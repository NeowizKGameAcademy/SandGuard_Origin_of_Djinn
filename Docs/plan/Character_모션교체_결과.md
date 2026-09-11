# 캐릭터별 다운로드 모션 적용

2026-09-09: Player 18개, Enemy 5종 각 4개(총 38개)를 Docs/model-art 각 캐릭터 폴더의 FBX로 교체했습니다. 기존 에셋 GUID와 클립 이름을 유지했습니다.

- 소스 사본: Assets/MotionLibrary/CharacterDownloads
- 전체 원본 → 적용 파일 대응: Logs/character-motion-replacement.md
- 검사병: Slash, 암살자: Standing Torch Melee Attack Stab / Run, 망치병: Great Sword Walk 선택.
- 미사용 추가 모션(Slash2 등)은 원본 폴더에 보관되어 있습니다.
- 기존 팩으로 되돌아가지 않도록 Player/Enemy 생성 도구의 소스 경로도 갱신했습니다.

## Avatar 기준 자세 통일

추가 사용자 승인 후 Player와 Assassin에 적용했습니다. FBX별 다리뼈 길이가 달라 하나의 Avatar를 그대로 복사하면 Unity가 불일치 오류를 내므로, 각 파일의 뼈 위치·길이는 보존하고 기준 자세 회전을 통일했습니다. Player는 Standing Run Forward, Assassin은 Run 다운로드를 기준으로 사용합니다.

- 대상: Player 몸체 + 17개 모션, Assassin 몸체 + 4개 모션.
- 원본 FBX 바이트·메시·스킨 가중치는 수정하지 않았습니다.
- 메뉴: SandGuard > Animation Library > Unify Player and Assassin Avatars.
- Apply Character Downloads 재실행 시에도 같은 기준을 다시 적용합니다.
- Player 8방향 이동의 원본 자체 Avatar 대비 발끝 방향 차이는 최대 약 0.5도, Assassin 이동은 약 0.13도입니다.
- 일부 대기·공중 모션의 원래 자동 Avatar 기준과는 차이가 남으므로 이 수치를 전체 모션의 시각적 완전 일치로 해석하지 않습니다.
- 연결 내역: Logs/character-shared-avatars.txt
- 실제 게임 동작 검증: Logs/unified-avatar-tests.xml

검증 결과: 이동·점프·대시·Enemy 손/장비 테스트 8/8 통과. 추가 마법 시전 테스트는 2/7 통과했습니다. 단일 진단에서 입력=True, Alive, CombatEnabled=True였지만 PlayerBasicAttack.projectilePrefab이 런타임 null이어서 시전이 시작되지 않았습니다. Avatar 통일과 별도로 탄환 프리팹 참조 확인이 필요합니다. 결과: Logs/unified-avatar-casting-tests.xml, Logs/unified-cast-diagnostic.xml.

## 시전 문제 해결

PlayerBolt 에셋을 강제로 다시 가져온 뒤 PlayerBasicAttack에 실제 PlayerProjectile 컴포넌트를 다시 연결·저장했습니다. PlayerSetupBuilder도 프리팹 GameObject에서 컴포넌트를 읽도록 수정했습니다. 저장된 참조는 존재했으나 이전 실행에서는 null로 해석되었으며, 재가져오기·재저장 후 시전 테스트 7/7이 통과했습니다. 세부 결과: Logs/projectile-repair-tests.xml. 이동 중 발사, 짧은 클릭, 공중 시전, 시전 변형, 손바닥 발사 위치, 피격·사망 전환을 포함합니다.

## HammerBrute 이동 비교 및 공격 변경

Great Sword Walk 원본과 HammerBrute_Move.fbx의 바이트는 동일합니다. 클립 0.600초, 이동 상태 배율 0.571818, 순수 이동 상태 한 주기는 약 1.049초입니다. 동일 모션을 원본과 게임 몸체에 적용한 21개 시점 비교에서 발끝 방향 차이는 최대 약 1.004도입니다. 이동 설정은 변경하지 않았습니다.

공격은 bandit-hammerer의 @Heavy Weapon Swing.fbx로 교체했습니다. 기존 에셋 GUID/Attack 클립 이름과 공격 상태 재생 시간 1.833초를 유지했습니다. 새 클립은 1.267초, 상태 재생 배율 0.690909입니다. 공격 자세의 원본 대비 발끝 방향 차이는 최대 약 1.646도입니다. 추가 양손 손잡이 검증은 열린 Unity의 창 접근/배치 실행이 불가능해 완료하지 못했습니다. 실행 예약은 해제했습니다.

## HammerBrute Move 조기 반복 수정

원본 FBX를 Blender에서 독립적으로 확인한 결과 30fps, 프레임 1~42(41프레임 간격, 약 1.3667초)였습니다. 기존 Unity 클립은 0~18만 재생해 끝 동작이 잘리고 있었습니다. HammerBrute_Move.fbx.meta의 lastFrame을 41로 수정했습니다. 이동 배율 0.571818은 유지하므로 수정 후 순수 이동 상태의 한 주기는 약 2.390초입니다.

원인은 교체 도구가 기존 대상 Importer의 캐시된 defaultClipAnimations에서 구간을 읽은 것입니다. 새 파일을 독립적으로 가져온 Importer에서 구간을 읽도록 수정했습니다. Unity 에디터의 현재 창에 접근할 수 없어, 전체 구간 길이와 원본 길이를 비교하는 읽기 전용 검증을 다음 스크립트 로드 때 실행하도록 예약했습니다. 검증 결과 파일: Logs/hammer-walk-range-verification.txt.

## HammerBrute 공격 전체 구간 수정 및 검증 완료

Heavy Weapon Swing 원본을 Blender로 독립 확인: 30fps, 프레임 1~157, 길이 5.2초. 이전 적용은 0~38프레임만 재생하고 있었습니다. 0~156으로 수정했고 Attack→Locomotion 전환은 진행률 1.0 이후, 전환 시간 0.05초로 변경했습니다. 전체 동작을 기존 공격 상태 시간 약 1.833초에 맞추도록 2.836364배속을 적용했습니다. 생성 도구에도 이 설정을 반영했습니다.

41개 시점에서 양손-손잡이 최대 간격 0.00000m. 플레이 모드 HammerBrute 장비·동작 테스트와 전체 클립/끝까지 재생 후 복귀 회귀 테스트 2/2 통과. 결과: Logs/hammer-full-attack-tests.xml.
