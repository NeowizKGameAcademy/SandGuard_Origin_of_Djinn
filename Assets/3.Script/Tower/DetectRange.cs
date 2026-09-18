using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectRange : MonoBehaviour
{


    [SerializeField] private TowerStatus Status;

    [SerializeField] private float Range = 20f;

    public bool IsDetecting => Detect_Count > 0;
    public int DetectCount => Detect_Count;
    public HashSet<ICombatTarget> enemies => Detect_Counted;
    public float range => Range;

    private int Detect_Count;

    private readonly Collider[] Detect_Buffer = new Collider[64];
    private readonly HashSet<ICombatTarget> Detect_Counted = new();

    private ICombatTarget Owner;

    private void Awake()
    {
        TryGetComponent(out Status);
        Owner = GetComponentInParent<ICombatTarget>();
    }

    private void Update()
    {
        Range = Status.detectRange;

        CountEnemies();
    }

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

    public bool Contains(Collider other)
    {
        if (other == null)
            return false;

        Vector3 center = transform.position;

        return (other.ClosestPoint(center) - center).sqrMagnitude <= Range * Range;
    }

    public bool Contains(Vector3 position)
    {
        return (position - transform.position).sqrMagnitude <= 7.5f * 7.5f;
    }
}
