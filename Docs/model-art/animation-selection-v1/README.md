# 적 애니메이션 선정 v1

2026-09-09 · 현재 제작한 장비와 외형 기준으로 선정했습니다. **암살자는 쌍단검, 방패병은 짧은 검+타워 방패, 우두머리는 방패 없는 곡도+망토**입니다. 이전 계획의 한손 단검·곡괭이·보스 방패 제안은 이 선정에 적용하지 않았습니다.

**선정 완료는 게임 연결 완료를 뜻하지 않습니다.** core는 대기/이동/공격/사망의 첫 적용 후보, support는 방어·피격·추가 공격 후보입니다. 모든 원본은 보존했고, 고른 FBX만 캐릭터별 폴더에 용도 이름으로 복사했습니다. FBX 내용·재생 속도·루프·타격 이벤트는 수정하지 않았습니다.

## 캐릭터별 선정

### 검병 · 짧은 검 + 원형 방패

| 용도 | 우선순위 | 원본 (`Docs/model-art/` 기준) | 길이 | 선정 이유·남은 보정 |
|---|---|---|---:|---|
| Idle | core | `Pro Sword and Shield Pack/sword and shield idle (4).fbx` | 2.53초 | 짧고 안정적인 무장 대기. 긴 몸풀기 idle 대신 선택. |
| Move | core | `Pro Sword and Shield Pack/sword and shield run.fbx` | 0.70초 | 전진 달리기. 이동 속도에 맞춰 재생 속도 조정. |
| Attack | core | `Pro Sword and Shield Pack/sword and shield attack (4).fbx` | 1.00초 | 전진량이 작은 1초 검 공격. 기본 공격 후보. |
| Death | core | `Pro Sword and Shield Pack/sword and shield death.fbx` | 2.30초 | 뒤로 쓰러지는 검·방패 자세. |
| Walk | support | `Pro Sword and Shield Pack/sword and shield walk.fbx` | 1.07초 | 순찰용 전진 걷기. |
| Hit | support | `Pro Sword and Shield Pack/sword and shield impact (3).fbx` | 0.70초 | 짧은 피격 후보. 전신 반응 연결은 별도. |
| GuardStart | support | `Pro Sword and Shield Pack/sword and shield block.fbx` | 0.47초 | 방패를 앞으로 드는 진입 동작. |
| GuardHold | support | `Pro Sword and Shield Pack/sword and shield block idle.fbx` | 1.40초 | 방패를 든 자세 유지. |
| GuardEnd | support | `Pro Sword and Shield Pack/sword and shield block (2).fbx` | 0.50초 | 방패를 내리고 전투 대기로 복귀. 피격 반응으로 쓰지 않음. |

### 암살자 · 쌍단검

| 용도 | 우선순위 | 원본 (`Docs/model-art/` 기준) | 길이 | 선정 이유·남은 보정 |
|---|---|---|---:|---|
| Idle | core | `Knife Idle.fbx` | 4.47초 | 단검 대기 후보. 왼손에도 단검을 유지하므로 손가락·칼끝 방향 보정 필요. |
| Move | core | `Ninja Run.fbx` | 0.63초 | 몸을 낮추고 팔을 뒤로 뺀 제자리 달리기. 쌍단검 간섭 확인 필요. |
| Attack | core | `Double Dagger Stab.fbx` | 3.07초 | 쌍단검 공격 원본. 3.07초이므로 짧은 공격 구간 편집 또는 긴 콤보로 구성 필요. 현재 1.2초 간격에 그대로 연결하지 않음. |
| Death | core | `Pro Magic Pack/Standing React Death Left.fbx` | 3.60초 | 방패 동작이 없는 옆으로 쓰러지는 공용 후보. 단검과 바닥 간섭 확인 필요. |
| Sneak | support | `Sneaking Forward.fbx` | 1.67초 | 잠입·느린 접근에만 사용. 추격 기본 이동은 Ninja Run. |
| AttackQuick | support | `Stabbing (1).fbx` | 2.13초 | 오른손 찌르기 보조 후보. 왼손 단검은 유지. 이 클립도 2.13초라 구간 편집 필요. |
| Hit | support | `Pro Magic Pack/Standing React Small From Front.fbx` | 1.20초 | 방패 없는 정면 피격 공용 후보. |

### 방패병 · 짧은 검 + 타워 방패

| 용도 | 우선순위 | 원본 (`Docs/model-art/` 기준) | 길이 | 선정 이유·남은 보정 |
|---|---|---|---:|---|
| Idle | core | `Pro Sword and Shield Pack/sword and shield block idle.fbx` | 1.40초 | 방패를 앞에 세운 경계 자세로 검병과 구분. |
| Move | core | `Pro Sword and Shield Pack/sword and shield walk.fbx` | 1.07초 | 무거운 전진 걷기. 현재 공통 3.5m/s를 그대로 쓸 경우 발 미끄러짐 조정 필요. |
| Attack | core | `Pro Sword and Shield Pack/sword and shield attack (4).fbx` | 1.00초 | 큰 도약 없는 짧은 검 공격. 타워 방패와 오른팔 궤적 간섭 확인 필요. |
| Death | core | `Pro Sword and Shield Pack/sword and shield death.fbx` | 2.30초 | 뒤로 쓰러짐. 큰 방패의 몸·바닥 간섭 확인 필요. |
| Run | support | `Pro Sword and Shield Pack/sword and shield run.fbx` | 0.70초 | 빠른 추격이 필요할 때 걷기 대신 사용. |
| Hit | support | `Pro Sword and Shield Pack/sword and shield impact (3).fbx` | 0.70초 | 피격 후보. 방패 방어 성공 반응과 구분. |
| GuardStart | support | `Pro Sword and Shield Pack/sword and shield block.fbx` | 0.47초 | 공격 후 방패를 다시 드는 전환 후보. |
| GuardEnd | support | `Pro Sword and Shield Pack/sword and shield block (2).fbx` | 0.50초 | 방패 자세에서 공격 준비 자세로 복귀. |

### 망치병 · 양손 대형 망치

| 용도 | 우선순위 | 원본 (`Docs/model-art/` 기준) | 길이 | 선정 이유·남은 보정 |
|---|---|---|---:|---|
| Idle | core | `Great Sword Pack/great sword idle.fbx` | 2.00초 | 양손 무기 대기. 왼손을 망치 보조 손잡이에 맞출 것. |
| Move | core | `Great Sword Pack/great sword run (2).fbx` | 0.60초 | 전진 달리기. 번호 없는 run은 후진이라 제외. |
| Attack | core | `Great Sword Pack/great sword slash (3).fbx` | 1.83초 | 상체를 크게 숙이며 양손으로 내려치는 강타 후보. 1.83초. 망치 머리 궤적·왼손 IK 보정 필요. |
| Death | core | `Great Sword Pack/two handed sword death.fbx` | 2.40초 | 양손 무기를 든 뒤로 쓰러짐 후보. |
| Walk | support | `Great Sword Pack/great sword walk.fbx` | 1.37초 | 느린 접근·순찰용 전진 걷기. |
| AttackQuick | support | `Great Sword Pack/great sword attack.fbx` | 1.20초 | 1.2초의 짧은 양손 공격 후보. 강타와 구분되는 보조 공격. |
| Hit | support | `Great Sword Pack/great sword impact (3).fbx` | 1.13초 | 양손 무기 피격 후보. |

### 우두머리 · 한손 곡도 + 망토, 방패 없음

| 용도 | 우선순위 | 원본 (`Docs/model-art/` 기준) | 길이 | 선정 이유·남은 보정 |
|---|---|---|---:|---|
| Idle | core | `Pro Sword and Shield Pack/sword and shield idle (4).fbx` | 2.53초 | 안정적인 전투 대기 원본 후보. 방패를 드는 왼팔 자세를 빈손 경계 자세로 수정해야 함. |
| Move | core | `Pro Sword and Shield Pack/sword and shield walk.fbx` | 1.07초 | 천천히 접근하는 후보. 왼팔 자세 수정 및 이동 속도 조정 필요. |
| Attack | core | `Pro Sword and Shield Pack/sword and shield slash.fbx` | 1.50초 | 제자리 한손 베기 후보. 왼팔 방패 자세를 빈손 균형 자세로 수정해야 함. 1.5초. |
| Death | core | `Pro Magic Pack/Standing React Death Backward.fbx` | 3.63초 | 방패 없는 뒤로 쓰러지는 공용 후보. 망토 눌림·바닥 간섭 보정 필요. |
| AttackAlt | support | `Pro Sword and Shield Pack/sword and shield slash (3).fbx` | 1.57초 | 제자리 다른 베기 후보. 왼팔 자세 보정 후 패턴 확장에 사용. |
| Kick | support | `Pro Sword and Shield Pack/sword and shield kick.fbx` | 1.20초 | 근접 밀어내기 후보. 별도 판정·공격 패턴과 왼팔 보정 필요. |
| Hit | support | `Pro Magic Pack/Standing React Small From Front.fbx` | 1.20초 | 방패 없는 정면 피격 공용 후보. |
| Run | support | `Pro Sword and Shield Pack/sword and shield run.fbx` | 0.70초 | 추격용 예비 후보. 왼팔 방패 자세 보정 필요. |

## 적용 시 필요한 작업

- 현재 `EnemyVisuals`는 `Speed`, `Attack`, `Die`만 전달합니다. Guard/Hit/추가 공격은 클립을 골랐을 뿐 게임 로직이 연결된 기능은 아닙니다.
- `EnemyMeleeAttack` 기본 공격 간격은 1.2초입니다. 암살자 3.07초·망치병 1.83초·우두머리 1.50초 공격은 구간 편집/재생 속도/공격 간격을 함께 맞춰야 합니다. 손목 속도나 6개 샘플만으로 타격 시점을 확정하지 않았으며 무기 장착 후 판정 시점을 잡습니다.
- 망치병은 대검 모션을 망치로 전용하는 후보입니다. 양손 간격과 망치 머리의 무게감을 맞추고, 왼손 IK와 손잡이 위치를 확인해야 합니다.
- 우두머리는 현재 라이브러리에 맞는 방패 없는 한손 검 세트가 부족합니다. 검+방패 원본에서 왼팔을 수정하는 조건으로 선정했습니다. 엄지 본이 없는 리그의 그립 보정과 독립 망토 움직임도 별도입니다.
- 기존 미리보기의 몸체는 정적입니다. 실제 리깅 몸체로 교체하고 Humanoid 리타게팅을 검증해야 합니다. 클립의 소스 골격에 맞는 Avatar를 사용하고, 골격이 다른 대상 캐릭터의 Avatar를 무조건 복사 지정하지 않습니다.
- 이동은 현재 EnemyMotor가 담당하므로 루트 모션 추출·적용 설정과 발 속도를 검증합니다. 여기의 이동량은 소스 골격의 Blender 좌표 단위이며 대상 캐릭터의 미터 단위 속도가 아닙니다.
- Loop 표시는 반복 후보입니다. 첫/끝 자세와 블렌드 경계를 확인한 후 Unity에 적용합니다. Death는 반복하지 않고 마지막 자세를 유지합니다.

## 제외·보류한 파일

- `Pro Sword and Shield Pack`과 일반 팩의 같은 이름 49개는 31개 시간점의 본 위치 샘플이 일치했습니다. 바이트가 동일한 파일은 아니며 모든 곡선의 완전 동일성 검증은 아닙니다. 검사 후 일반 팩 폴더가 없어져서 현재 존재하는 Pro 팩으로 통일했습니다. 이 작업에서는 원본을 이동하거나 삭제하지 않았습니다.
- `Capoeira`와 `Zombie Stand Up`이 붙은 몸체 FBX는 리깅 소스로만 사용합니다. 기본 전투 애니메이션에는 포함하지 않았습니다.
- 망치병의 번호 없는 `great sword run.fbx`, 검+방패의 `run (2).fbx`는 후진이므로 전진 이동에서 제외했습니다.
- 검+방패 `attack.fbx`, `attack (2)`, `attack (3)`, `slash (2)`, 대검 점프·회전·슬라이드 공격은 전진량이나 동작 크기가 커서 기본 공격에서 보류했습니다.
- 긴 몸풀기 idle, 발도·납도, 시전 동작은 기본 전투 세트에서 제외했습니다. Magic 팩에서는 방패 없는 피격·사망만 재사용합니다.

## 검증 자료

- 총 213개 소스 애니메이션을 읽어 길이·뼈 수·이동량을 측정했습니다. 선정 복사본 39개의 SHA-256이 원본과 일치합니다.
- `selection.json`: 캐릭터별 원본 경로, 복사 경로, 길이, 우선순위, 보정 사항.
- `library-audit.json`: 전체 라이브러리 측정값과 관절 샘플. `*-1.png` 등의 시트는 6개 시점의 관절 자세 비교이며 실제 캐릭터/장비 렌더가 아닙니다.
- 재생성: Blender에서 `verification/audit_animation_library.py`, Pillow가 있는 Python에서 `verification/draw_animation_contacts.py`, `verification/select_enemy_animations.py` 순서로 실행합니다.
