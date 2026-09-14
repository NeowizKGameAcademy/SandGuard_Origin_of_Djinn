# 코어 파괴 VFX

프리팹: `Prefabs/VFX_Core_Destruction.prefab`

참고 원본: `Assets/2.Model/Prefabs/Core Base.prefab`. 원본은 8방향으로 돌출된 복셀 코어이며, 원본 상부 메시의 삼각형을 공간상 8구역으로 나눠 파편 메시를 만든다. 컷 면을 새로 막는 물리 절단은 아니며 양면 렌더링으로 사용한다. 받침대와 원본 프리팹 에셋은 수정하지 않는다.

- 0~0.2초: 원형 그대로의 대체 코어가 밝아지고 약간 수축하며 표면 균열이 점멸한다.
- 0.2초: 대체 코어가 사라지고 청록·백색 섬광, 지면 충격파, 작은 입자가 방출된다.
- 0.2~1.4초: 실제 원본 메시에서 만든 8개 파편이 퍼지고 회전하며 내려앉는다.
- 1.4~1.8초: 파편이 어두운 청록색으로 식는다. 마지막에는 받침대와 꺼진 파편만 남는다.

## 게임 연결

Core Base 루트와 같은 위치·회전·스케일에 스폰하고 `VfxCoreDestruction.BindTarget(coreBase.transform)`을 호출한다. 원본의 `Core`(하위 조명 포함)는 대체 메시로 교체되어 즉시 숨겨지고, `Circle Effect`는 0.2초 파열 시 숨겨진다. 루트의 받침대 `Mesh`는 유지된다. 피해 판정·게임 오버·UI는 포함하지 않는다.

VFX를 반환하거나 제거해도 원본 코어는 자동으로 살아나지 않는다. 리스폰이나 전시 반복에서만 `RestoreTarget()`을 명시적으로 호출한다. 같은 원본에 중복 바인딩하지 않는다. 재사용 전에 `Restart()`와 파티클 재시작을 수행한다. 게임에서는 약 3초 후 반환하면 되고, 그때 파편도 함께 제거된다.

파편 궤적은 원본 받침대와 평평한 바닥을 기준으로 계산한 시각 연출이다. 벽·경사면과 물리 충돌하지 않는다. 기본 크기에서 코어는 중심 (0,5.5,0), 크기 (8,8,8), 받침대 크기는 (11,2.3,11)이다.

## 보기 및 재생성

- `DesertTower > VFX > Build Core Destruction (Core Base)`
- `DesertTower > VFX > Build Core Destruction + Preview`
- Showcase의 `NEW 09 Core Destruction`: 4초마다 반복하며, 전시 공간에 맞춰 원본과 VFX를 함께 0.18배로 보여준다.
- `Docs/vfx-preview/CoreDestruction/`: 실제 Unity 렌더 60프레임과 단계 전환 검증 결과.

기존 `VFX_Core_Damage_Flash`는 일반 피격용으로 유지한다.
