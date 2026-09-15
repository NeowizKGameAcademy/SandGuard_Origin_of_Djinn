using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// [통합 수정 2026-09-14] 게임의 실제 적(Assets/Enemy)과 연결하기 위한 수정입니다.
//
// 무엇을 바꿨나
//   - 트리거(OnTriggerEnter/Exit)로 적의 EnemyState에 "탐지됨"을 기록하던 부분을 껐습니다.
//   - 대신 "이 콜라이더가 탐지 범위 안인가"를 바로 묻는 Contains()를 추가했습니다.
//     RangeController가 공격·둔화 전에 이걸로 탐지 여부를 확인합니다.
//   - 사거리 값(Range)과 구체 반경 맞추기(SetRange)는 그대로입니다.
//
// 왜 바꿨나
//   - EnemyState는 임시 적 전용이라 실제 적에는 없습니다. 적의 상태를 타워가 적어 넣는 대신,
//     타워는 범위만 판단하고 적에게는 "피해/둔화 요청"만 보내는 구조로 맞췄습니다.
//   - 실제 적은 Rigidbody 없이 NavMeshAgent로 움직입니다. Rigidbody가 없는 탐지 구체(오벨리스크)와는
//     트리거 이벤트가 아예 발생하지 않습니다.
//   - 적이 오브젝트 풀로 돌아가며 비활성화될 때는 OnTriggerExit이 호출되지 않아
//     "탐지됨" 기록이 남는 문제가 있습니다. 매번 거리로 확인하면 이런 기록이 필요 없습니다.
//
// 기존 코드는 지우지 않고 아래에 주석으로 남겼습니다.
// =====================================================================================
public class DetectRange : MonoBehaviour
{
    [SerializeField] private float Range = 20f;
    [SerializeField] private SphereCollider Detect_Range;

    private HashSet<EnemyState> detectedEnemies = new();

    // [통합 수정 2026-09-15] 코브라 불꽃(FireOnOff)·타워 색(TowerOnOff)이 켜지지 않던 문제.
    //   기존 값은 트리거로 채우던 detectedEnemies 개수였는데, 위 설명대로 트리거를 꺼서 항상 0(= 탐지 안 됨)이었습니다.
    //   이제 매 프레임 범위 안의 "공격 가능한 적" 수를 직접 세어 넣습니다. 쓰는 쪽 코드는 그대로 동작합니다.
    /* 기존 코드
    public bool IsDetecting => detectedEnemies.Count > 0;
    public int DetectCount => detectedEnemies.Count;
    */
    public bool IsDetecting => Detect_Count > 0;
    public int DetectCount => Detect_Count;

    public float range => Range;

    // [통합 추가] 이번 프레임에 범위 안에 있는 적 수, 겹침 검사 버퍼, 같은 편 구분용 타워 자신
    private int Detect_Count;
    private readonly Collider[] Detect_Buffer = new Collider[64];
    private readonly HashSet<ICombatTarget> Detect_Counted = new();
    private ICombatTarget Owner;

    private void Awake()
    {
        TryGetComponent(out Detect_Range);
        Owner = GetComponentInParent<ICombatTarget>(); // [통합 추가]
    }

    private void Update()
    {
        Range = Mathf.Clamp(Range, 0f, 50f);

        SetRange();
        CountEnemies(); // [통합 추가]
    }

    // [통합 추가] 범위 구체와 겹치는 콜라이더 중 공격 가능한 다른 편 전투 대상 수를 센다(트리거는 무시).
    //   몸에 콜라이더가 여러 개인 적은 한 번만 센다. Contains()와 같은 기준(몸 가장자리까지)이다.
    private void CountEnemies()
    {
        string Faction = Owner != null ? Owner.FactionId : "Ally";
        int Count = Physics.OverlapSphereNonAlloc(transform.position, Range, Detect_Buffer, ~0, QueryTriggerInteraction.Ignore);

        Detect_Counted.Clear();

        for (int i = 0; i < Count; i++)
        {
            ICombatTarget Enemy = Detect_Buffer[i].GetComponentInParent<ICombatTarget>();

            if (Enemy != null && Enemy.IsTargetable && Enemy.FactionId != Faction)
                Detect_Counted.Add(Enemy);
        }

        Detect_Count = Detect_Counted.Count;
    }

    private void SetRange()
    {
        if (Detect_Range == null)
            return;

        Detect_Range.radius = Range;
    }

    // [통합 추가] 콜라이더의 가장 가까운 점이 탐지 범위(이 오브젝트 중심에서 Range) 안인지.
    // 트리거가 "겹쳤다"고 판단하던 기준과 같게, 적의 중심이 아니라 몸의 가장자리까지 본다.
    public bool Contains(Collider other)
    {
        if (other == null)
            return false;

        Vector3 center = transform.position;

        return (other.ClosestPoint(center) - center).sqrMagnitude <= Range * Range;
    }

    /* 기존 코드: 트리거로 EnemyState에 탐지 기록. 위 설명의 이유로 사용하지 않는다.
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out EnemyState enemyState))
        {
            detectedEnemies.Add(enemyState);
            enemyState.Detected(this, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out EnemyState enemyState))
        {
            detectedEnemies.Remove(enemyState);
            enemyState.Detected(this, false);
        }
    }
    */
}
