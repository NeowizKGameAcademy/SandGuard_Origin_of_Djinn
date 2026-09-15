using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// [통합 수정 2026-09-14] 게임의 실제 적(Assets/Enemy)과 연결하기 위한 수정입니다.
//
// 원칙: 타워는 "누구에게 무엇을 할지"만 정하고, 적에게는 요청만 보냅니다.
//       피해를 받은 뒤의 체력·방어(방패)·피격 연출·사망, 둔화가 걸린 뒤의 이동 속도는 적이 스스로 처리합니다.
//
// 무엇을 바꿨나
//   1) OnTriggerStay/Exit → Update에서 Tick_Interval마다 이 오브젝트의 콜라이더 모양 그대로 겹침 검사
//   2) 탐지 확인: EnemyState.IsDetectedBy(DetectRange) → DetectRange.Contains(적 콜라이더)
//   3) Fire: EnemyState.GetDamage()(색만 바뀌는 연출) → IDamageable.TakeDamage(DamageInfo)(실제 피해 요청)
//   4) Slow: EnemyState.SetSlow/RemoveSlow → ISlowable.Slow(세기, 짧은 지속시간)을 검사 때마다 갱신
//
// 왜 바꿨나
//   - EnemyState는 임시 적 전용입니다. 실제 적은 IDamageable(피해 받기), ISlowable(둔화 받기)을 구현합니다.
//   - 트리거 이벤트는 둘 중 하나에 Rigidbody가 있어야 발생합니다. 실제 적은 Rigidbody가 없어서
//     Rigidbody 없는 오벨리스크의 Slow Range에서는 이벤트가 오지 않습니다.
//   - OnTriggerStay는 물리 프레임마다 불려서 "몇 초에 얼마" 같은 피해 간격을 정할 수 없습니다.
//     또 타워가 정지(비활성)되어도 트리거 콜백은 계속 옵니다. Update 방식은 컴포넌트를 끄면 멈춥니다.
//   - 둔화는 "범위를 나갈 때 해제" 대신 "짧게 걸고 계속 갱신"합니다. 적이 풀로 돌아가거나 죽어서
//     OnTriggerExit이 오지 않아도, 갱신이 끊기면 스스로 풀립니다.
//   - SlowRatio는 "남는 속도 비율"(0.5 = 절반 속도)이고, ISlowable은 "줄이는 양"(0.5 = 절반 속도)을 받습니다.
//     그래서 1 - SlowRatio를 넘깁니다. 기존 인스펙터 값의 의미는 그대로입니다.
//   - 피해 요청에 공격 방향·원인 ID("tower." 로 시작)를 담습니다. 방패병 정면 방어와
//     보스(자히르)의 "정면 타워 피해 감소"가 이 값으로 판단합니다.
//
// 새로 생긴 인스펙터 값(Fire_Damage, Tick_Interval, Cause_Id, Target_Mask)은 임시 수치입니다. 밸런스에 맞게 조정해 주세요.
// 기존 코드는 지우지 않고 아래에 주석으로 남겼습니다.
// =====================================================================================
public class RangeController : MonoBehaviour
{
    public enum Type
    {
        Fire,
        Slow
    }

    [SerializeField] private Type type;

    [Header("Detect")]
    [SerializeField] private DetectRange DetectRange;

    [Header("Slow")]
    [SerializeField] private float SlowRatio = 0.5f;
    private SphereCollider SlowRange;

    private void OnEnable()
    {
        TryGetComponent(out SlowRange);

        if (type.Equals(Type.Slow))
        {
            SlowRange.radius = DetectRange.range;
        }
    }

    // [통합 추가] 검사·요청 간격(초). 둔화는 이 간격의 2배 조금 넘게 걸어 두고 계속 갱신한다.
    [Header("Integration")]
    [SerializeField] private float Tick_Interval = 0.25f;
    // [통합 추가] Fire: 한 번 검사할 때 주는 피해(임시 수치. 0.25초마다 5 = 초당 20)
    [SerializeField] private float Fire_Damage = 5f;
    // [통합 추가] 피해 원인 ID. 적 쪽 방어 규칙이 "tower." 접두사로 타워 공격을 구분한다.
    [SerializeField] private string Cause_Id = "tower.fire";
    // [통합 추가] 적 콜라이더를 모을 레이어. 기본은 전부.
    [SerializeField] private LayerMask Target_Mask = ~0;

    private Collider Area;
    private ICombatTarget Owner;
    private float Tick_Timer;
    private readonly Collider[] Buffer = new Collider[64];
    private readonly HashSet<Guid> Handled = new();

    private string Faction => Owner != null ? Owner.FactionId : "Ally";

    // [통합 추가] 범위 모양(이 오브젝트의 콜라이더)과 타워 자신의 전투 정보(진영·ID)
    private void Awake()
    {
        TryGetComponent(out Area);
        Owner = GetComponentInParent<ICombatTarget>();
    }

    // [통합 추가] 트리거 콜백 대신 주기적으로 범위 안 적을 찾아 요청을 보낸다.
    private void Update()
    {
        Tick_Timer -= Time.deltaTime;

        if (Tick_Timer > 0f)
            return;

        Tick_Timer = Tick_Interval;

        int Count = OverlapArea();
        Handled.Clear();

        for (int i = 0; i < Count; i++)
        {
            Collider Other = Buffer[i];
            ICombatTarget Enemy = Other.GetComponentInParent<ICombatTarget>();

            // 전투 대상이 아니거나, 이미 이번 검사에서 처리했거나(몸에 콜라이더가 여러 개), 공격 불가, 같은 편이면 건너뛴다.
            if (Enemy == null || !Handled.Add(Enemy.EntityId) || !Enemy.IsTargetable || Enemy.FactionId == Faction)
                continue;

            // 기존 규칙 유지: 탐지 범위 안에 있는 적에게만 적용한다.
            if (DetectRange != null && !DetectRange.Contains(Other))
                continue;

            switch (type)
            {
                case Type.Fire:

                    SendDamage(Enemy);

                    break;


                case Type.Slow:

                    ISlowable Slowable = Other.GetComponentInParent<ISlowable>();

                    if (Slowable != null)
                        Slowable.Slow(1f - SlowRatio, Tick_Interval * 2f + 0.05f);

                    break;
            }
        }
    }

    // [통합 추가] 피해 요청. 방향은 탐지 중심(타워)에서 적 몸통으로.
    private void SendDamage(ICombatTarget Enemy)
    {
        if (Enemy.DamageReceiver == null)
            return;

        Vector3 Origin = DetectRange != null ? DetectRange.transform.position : transform.position;
        Vector3 Point = Enemy.HitPosition;
        Vector3 Direction = Point - Origin;
        Direction = Direction.sqrMagnitude > 0.0001f ? Direction.normalized : transform.forward;

        Enemy.DamageReceiver.TakeDamage(new DamageInfo(Fire_Damage, Faction, Owner != null ? Owner.EntityId : (Guid?)null, Cause_Id, Point, Direction));
    }

    // [통합 추가] 이 오브젝트 콜라이더의 모양·위치·회전·크기 그대로 겹침 검사. 트리거는 무시한다.
    private int OverlapArea()
    {
        switch (Area)
        {
            case BoxCollider Box:
            {
                Transform T = Box.transform;
                Vector3 Scale = T.lossyScale;
                Vector3 Half = Vector3.Scale(Box.size * 0.5f, new Vector3(Mathf.Abs(Scale.x), Mathf.Abs(Scale.y), Mathf.Abs(Scale.z)));

                return Physics.OverlapBoxNonAlloc(T.TransformPoint(Box.center), Half, Buffer, T.rotation, Target_Mask, QueryTriggerInteraction.Ignore);
            }

            case SphereCollider Sphere:
            {
                Transform T = Sphere.transform;
                Vector3 Scale = T.lossyScale;
                float Radius = Sphere.radius * Mathf.Max(Mathf.Abs(Scale.x), Mathf.Abs(Scale.y), Mathf.Abs(Scale.z));

                return Physics.OverlapSphereNonAlloc(T.TransformPoint(Sphere.center), Radius, Buffer, Target_Mask, QueryTriggerInteraction.Ignore);
            }

            case Collider Other:

                return Physics.OverlapBoxNonAlloc(Other.bounds.center, Other.bounds.extents, Buffer, Quaternion.identity, Target_Mask, QueryTriggerInteraction.Ignore);

            default:

                return 0;
        }
    }

    /* 기존 코드: 임시 적(EnemyState) 전용 트리거 처리. 위 설명의 이유로 사용하지 않는다.
    private void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent(out EnemyState enemyState))
            return;

        if (!enemyState.IsDetectedBy(DetectRange))
            return;

        switch (type)
        {
            case Type.Fire:

                enemyState.GetDamage();

                break;


            case Type.Slow:

                enemyState.SetSlow(this, SlowRatio);

                break;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out EnemyState enemyState))
            return;

        if (type == Type.Slow)
        {
            enemyState.RemoveSlow(this);
        }
    }
    */
}
