using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyState : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material[] Matts = new Material[2];

    private MeshRenderer meshRenderer;

    private HashSet<DetectRange> detectingRanges = new();

    private Dictionary<RangeController, float> slowSources = new();

    private bool IsDamage = false;

    public bool IsDetected => detectingRanges.Count > 0;
    public bool IsSlowed => slowSources.Count > 0;

    public float SlowRatio
    {
        get
        {
            if (slowSources.Count == 0)
                return 1f;

            float ratio = 1f;

            foreach (float slowRatio in slowSources.Values)
            {
                if (slowRatio < ratio)
                {
                    ratio = slowRatio;
                }
            }

            return ratio;
        }
    }

    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (Matts.Length > 0)
        {
            meshRenderer.material = Matts[0];
        }

        detectingRanges.Clear();
        slowSources.Clear();

        IsDamage = false;
    }

    public void Detected(DetectRange range, bool detected)
    {
        if (detected)
        {
            detectingRanges.Add(range);
        }
        else
        {
            detectingRanges.Remove(range);
        }
    }

    public bool IsDetectedBy(DetectRange range)
    {
        return detectingRanges.Contains(range);
    }

    public void GetDamage()
    {
        if (IsDamage)
            return;

        StartCoroutine(GetDamage_co());
    }

    private IEnumerator GetDamage_co()
    {
        IsDamage = true;

        if (Matts.Length > 1)
        {
            meshRenderer.material = Matts[1];
        }

        yield return new WaitForSeconds(0.2f);

        if (Matts.Length > 0)
        {
            meshRenderer.material = Matts[0];
        }

        yield return new WaitForSeconds(0.5f);

        IsDamage = false;
    }

    public void SetSlow(RangeController source, float ratio)
    {
        slowSources[source] = ratio;
    }

    public void RemoveSlow(RangeController source)
    {
        slowSources.Remove(source);
    }
}