using UnityEngine;

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
}