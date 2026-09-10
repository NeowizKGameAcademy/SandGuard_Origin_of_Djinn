using System.Collections;
using UnityEngine;

public class EnemyDamage : MonoBehaviour
{
    [SerializeField] private Material[] Matts = new Material[2];

    private MeshRenderer meshRenderer;

    private Color originalColor;

    private bool IsDamage = false;
    private bool IsSlowed = false;

    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.material = Matts[0];

        originalColor = meshRenderer.material.color;

        IsDamage = false;
        IsSlowed = false;
    }

    private IEnumerator GetDamage_co()
    {
        IsDamage = true;

        // 피격 효과
        meshRenderer.material = Matts[1];

        yield return new WaitForSeconds(0.2f);

        // 슬로우 중이면 파란색이 적용된 기본 머티리얼
        meshRenderer.material = Matts[0];

        if (IsSlowed)
        {
            meshRenderer.material.color = Color.Lerp(originalColor, Color.blue, 0.5f);
        }
        else
        {
            meshRenderer.material.color = originalColor;
        }

        yield return new WaitForSeconds(0.5f);

        IsDamage = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Effect Range"))
        {
            IsSlowed = true;

            meshRenderer.material.color = Color.Lerp(originalColor, Color.blue, 0.5f);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Damage Range") && !IsDamage)
        {
            StartCoroutine(GetDamage_co());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Effect Range"))
        {
            IsSlowed = false;

            meshRenderer.material.color = originalColor;
        }
    }
}