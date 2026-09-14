using UnityEngine;

public class DetectRange : MonoBehaviour
{
    [SerializeField] private float Range = 20f;
    [SerializeField] private SphereCollider Detect_Range;

    public float range => Range;

    private void Awake()
    {
        TryGetComponent(out Detect_Range);
    }

    private void Update()
    {
        Range = Mathf.Clamp(Range, 0f, 50f);

        SetRange();
    }

    private void SetRange()
    {
        if (Detect_Range == null)
            return;

        Detect_Range.radius = Range;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out EnemyState enemyState))
        {
            enemyState.Detected(this, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out EnemyState enemyState))
        {
            enemyState.Detected(this, false);
        }
    }
}