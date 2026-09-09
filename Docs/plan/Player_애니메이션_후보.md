# 플레이어 애니메이션 후보 — 2026-09-09

## 적용 현황

기본 마나 볼트에 Pro Magic Pack의 `Standing 1H Magic Attack 01`을 적용했다. `Great Sword Pack/spell cast`와 `Pro Sword and Shield Pack/sword and shield casting (2)`는 오른손용 미러링을 거친 비교용 시전으로 연결했다. 기존 상체 마스크·손바닥 발사점·조준 IK를 활용하며, 준비/반동에는 IK 가중치를 줄이고 발사 때 높인다.

Magic Pack의 전후좌우 Walk/Run 8개, `Standing React Small From Front`, `Standing React Death Backward`도 적용했다. 총 13개 클립을 `Assets/Player/Art/Protagonist/CombatAnimations`에 가져왔으며 `Player.prefab`과 Controller 연결을 완료했다. 이동 중 상체 시전, 상체 피격, 전신 사망으로 구분한다. 기존 점프·대시 연결은 이어서 사용한다. 착지 전용 동작·버프·강한 주문은 후속 후보로 남긴다.

미리보기 씬에서 B로 시전 3종, V로 걷기/달리기, K로 피격/사망을 확인한다. 재적용 메뉴는 `SandGuard > Player > Apply Animation Packs`이다. 적용 후 PlayMode 테스트 33개가 통과했으며 결과는 `Logs/player-animation-packs-tests.xml`, 주인공 적용 렌더링은 `Logs/player-casting-captures/`에 있다.

## 다른 팩에서 찾은 후보

경로는 `Docs/model-art/` 기준. 길이는 원본 분석 기록이며 주인공에 적용했을 때의 재생 시간을 확정한 값은 아니다.

| 후보 | 원본 길이 | 적용 제안 | 필요한 수정 |
|---|---:|---|---|
| `Great Sword Pack/spell cast.fbx` | 1.13초 | 짧은 마나 볼트·보조 주문의 비교 후보 | 관절 샘플상 왼팔 시전. 오른손용 미러링, 반대팔의 무기 유지 자세 수정. 기본 연사에는 발사 구간 편집 필요 |
| `Pro Sword and Shield Pack/sword and shield casting (2).fbx` | 1.03초 | 손을 앞으로 내미는 짧은 주문 후보 | 왼팔을 내미는 형태. 오른손용 미러링과 검·방패를 잡는 나머지 팔 자세 수정 |
| `Pro Sword and Shield Pack/sword and shield power up.fbx` | 2.33초 | 마나 회복·자기 강화·램프 활성화 연출 | 무기 자세를 빈손 동작으로 수정. 효과 발동 시점과 실제 기능은 별도 연결 |
| `Great Sword Pack/great sword power up.fbx` | 3.07초 | 더 무게감 있는 강화·궁극기 준비 후보 | 상체를 젖히는 동작을 캐릭터 성격에 맞춰 조절. 양손 무기 자세 제거 |
| `Pro Sword and Shield Pack/sword and shield casting.fbx` | 2.93초 | 지면 대상 주문·광역기 후보 | 팔을 크게 들고 몸을 낮추는 동작이므로 전신 재생이 어울림. 이동 허용 여부를 스킬 규칙에서 결정 |
| `Great Sword Pack/great sword casting.fbx` | 4.80초 | 큰 소환·광역 마법·궁극기 후보 | 양팔을 머리 위로 올린 뒤 크게 숙이는 긴 동작. 기본 공격에는 부적합; 과한 준비/복귀 구간 축소 |
| `Ninja Run.fbx` | 0.63초 | 몸을 숙인 빠른 대시 자세 후보 | 기본 달리기로 쓰면 암살자 느낌이 강함. 짧은 대시 구간에서 상체만 사용할지 비교; 공중 시전과 우선순위 결정 |
| `Great Sword Pack/great sword jump (2).fbx` | 0.83초 | 무릎을 당기는 짧은 점프 대안 | 무기 잡는 상체를 기존 공중 시전으로 대체. 현 Jumping과 발 위치/착지 연결을 비교 |

## 기존 Magic 후보와 비교

- `Standing 1H Magic Attack 01` (2.30초): 오른팔을 뻗는 구간이 비교적 명확해 기본 마나 볼트 우선 후보. 전체 동작 대신 준비/발사/회복 구간을 나눠 쓴다.
- `Standing 1H Magic Attack 02` (2.20초): 팔과 몸통의 회전이 더 보여 보조 공격/변형 동작으로 비교한다.
- `Standing 1H Magic Attack 03` (2.30초): 무릎과 팔의 큰 움직임이 있어 연사보다 강공격에 우선 비교한다.
- `Standing Run/Walk Forward/Back/Left/Right`: 무기 전용 팩보다 빈손 시전에 맞추기 쉬운 전투 이동 후보. 캐릭터 로컬 이동 방향으로 Blend Tree를 구성해야 옆·뒤로 이동하면서 조준을 유지할 수 있다.
- `Standing React Small…`와 `Standing React Death…`: 별도 무기 자세를 제거하는 작업이 적어 플레이어 피격·사망 후보로 유지한다.

현재 Attack Interval은 0.3초다. 1초짜리 짧은 클립도 그대로 매 발마다 재생하면 연사와 맞지 않는다. 기본 공격은 준비 → 시전 유지 → 짧은 발사/반동 → 종료로 구성하고, 원본에서 쓸 구간을 고른다. 발사 시점은 6개 관절 샘플만으로 확정하지 않는다.

## 우선 제외

- `great sword slide attack` (2.13초): 바닥을 낮게 지나가는 무기 공격이며 원본의 전진량도 크다. 현재 0.2초 대시에 통째로 연결하기보다 별도 슬라이딩 공격이 필요할 때 검토한다.
- 대검/검·방패의 일반 베기·점프 공격: 현재 손바닥 마법 플레이어에 바로 쓰기에는 무기 궤적이 강하다.
- 무기 팩의 일반 idle/run: 무기를 잡는 팔 자세가 남으므로 현재 빈손 이동 세트를 우선 유지한다. 필요하면 하체만 재사용한다.

## 확인 범위와 자료

후보 조사에서는 기존 `Docs/model-art/animation-selection-v1/library-audit.json`에서 현재 존재하는 원본 164개를 대조하고, 시전/특수 동작 후보 12개는 SHA-256을 원본과 비교한 뒤 6개 시점의 관절 샘플을 시각적으로 검토했다. 이동·피격의 다른 후보는 파일 목록과 분석 기록을 참고했다. 이후 적용한 13개는 유효한 Humanoid 임포트를 확인했고, 시전 3종은 주인공 모델의 손바닥 조준·발사 위치·램프 전환을 테스트와 렌더링으로 확인했다.

- 후보 원본 확인 기록: `Logs/player-animation-candidates/verified-candidates.json`
- 시전 관절 비교: `Logs/player-animation-candidates/casting-comparison.png`
- 특수 동작 관절 비교: `Logs/player-animation-candidates/skills-and-mobility.png`
- 비교 이미지 생성 스크립트: `Logs/review_player_animation_candidates.py`

위 후보 비교 이미지는 원본 골격 샘플이다. 주인공에게 적용한 렌더링은 `Logs/player-casting-captures/`에서 확인한다. `Docs/model-art`의 원본 파일은 변경하지 않았다.
