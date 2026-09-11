using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDamage : MonoBehaviour
{
    [SerializeField] private Material[] Matts = new Material[2];

    private MeshRenderer meshRenderer;
    private Color originalColor;

    private bool IsDamage = false;
    private bool IsSlowed = false;

    public bool is_Slowed => IsSlowed;

    // 현재 Detect Range 안에 들어와 있는 타워
    private HashSet<GameObject> detectedTowers = new HashSet<GameObject>();

    // 현재 Effect Range 안에 들어와 있는 타워
    private HashSet<GameObject> effectTowers = new HashSet<GameObject>();


    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.material = Matts[0];
        originalColor = meshRenderer.material.color;

        IsDamage = false;
        IsSlowed = false;

        detectedTowers.Clear();
        effectTowers.Clear();
    }


    private IEnumerator GetDamage_co()
    {
        IsDamage = true;

        meshRenderer.material = Matts[1];

        yield return new WaitForSeconds(0.2f);

        meshRenderer.material = Matts[0];

        yield return new WaitForSeconds(0.5f);

        IsDamage = false;
    }
}