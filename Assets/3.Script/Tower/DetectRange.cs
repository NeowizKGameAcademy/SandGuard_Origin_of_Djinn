using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectRange : MonoBehaviour
{
    [SerializeField] private float Range = 20f;
    [SerializeField] private SphereCollider Detect_Range;

    private HashSet<EnemyState> detectedEnemies = new();

    public bool IsDetecting => detectedEnemies.Count > 0;
    public int DetectCount => detectedEnemies.Count;

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
}