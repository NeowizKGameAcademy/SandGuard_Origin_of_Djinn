using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// [통합 수정 2026-09-14] 게임의 실제 적(Assets/Enemy)과 연결하기 위한 수정입니다.
//
// 무엇을 바꿨나
//   1) 적을 찾는 방법: 태그 "Enemy" 검색 → 사거리 안 콜라이더에서 ICombatTarget(공용 전투 대상) 찾기
//   2) 보관하는 대상: Transform만 → ICombatTarget도 함께 보관(target 속성은 그대로 Transform을 돌려줌)
//   3) 대상 유지 조건: 오브젝트 활성 여부만 → 공격 가능 여부(IsTargetable)까지 확인
//   4) 새 속성 aimPoint: 적의 몸통 기준점(HitPosition). 조준(FirePointAim)이 발밑 대신 이 점을 본다.
//
// 왜 바꿨나
//   - 실제 적 프리팹은 태그가 "Untagged"라서 태그 검색으로는 한 마리도 찾지 못합니다.
//     태그는 오브젝트당 하나뿐이라, 적마다 태그를 강제하는 대신 전투 대상 인터페이스로 찾습니다.
//   - 실제 적은 체력이 0이 되면 "죽는 중" 연출 동안 오브젝트가 켜져 있습니다.
//     activeInHierarchy만 보면 죽는 적을 계속 조준하므로 IsTargetable로 거릅니다.
//   - 같은 편(플레이어 등)은 조준하지 않도록 진영(FactionId)을 비교합니다.
//     타워 진영은 본체에 붙은 체력 컴포넌트(ICombatTarget)에서 읽고, 없으면 "Ally"로 봅니다.
//
// 기존 코드는 지우지 않고 아래에 주석으로 남겼습니다.
// =====================================================================================
public class FindEnemy : MonoBehaviour
{
    [Header("Auto Aim")]
    [SerializeField] private DetectRange Range;
    [SerializeField] private float SearchTime = 0.2f;

    // [통합 추가] 적 콜라이더를 모을 레이어. 기본은 전부.
    [SerializeField] private LayerMask Target_Mask = ~0;

    private float SearchTimer;

    private Transform Target_Transform;

    // [통합 추가] 조준 중인 전투 대상과, 진영 비교에 쓰는 타워 자신의 전투 정보
    private ICombatTarget Target_Combat;
    private ICombatTarget Owner;

    //Properties
    public Transform target => Target_Transform;
    // [통합 추가] 조준점. 대상이 없으면 타워 위치.
    public Vector3 aimPoint => Target_Combat != null ? Target_Combat.HitPosition : (Target_Transform != null ? Target_Transform.position : transform.position);
    // [통합 추가] 조준 중인 전투 대상(없으면 null)
    public ICombatTarget targetCombat => Target_Combat;

    private string Faction => Owner != null ? Owner.FactionId : "Ally";

    private void OnEnable()
    {
        TryGetComponent(out Range);
        Owner = GetComponentInParent<ICombatTarget>(); // [통합 추가]
    }

    private void Update()
    {
        SearchTimer -= Time.deltaTime;

        if (SearchTimer <= 0f)
        {
            FindClosestTarget();
            SearchTimer = SearchTime;
        }
    }

    private void FindClosestTarget()
    {
        float Closest_Distance = Range.range * Range.range;
        Transform Closest_Target = null;
        ICombatTarget Closest_Combat = null; // [통합 추가]

        foreach (ICombatTarget enemy in Range.enemies)
        {
            if (enemy == null || !enemy.IsTargetable || enemy.FactionId == Faction)
                continue;

            Transform Enemy_Transform = ((Component)enemy).transform;
            float Distance = (Enemy_Transform.position - transform.position).sqrMagnitude;

            if (Distance < Closest_Distance)
            {
                Closest_Distance = Distance;
                Closest_Target = Enemy_Transform;
                Closest_Combat = enemy;
            }
        }

        Target_Transform = Closest_Target;
        Target_Combat = Closest_Combat; // [통합 추가]
    }
}
