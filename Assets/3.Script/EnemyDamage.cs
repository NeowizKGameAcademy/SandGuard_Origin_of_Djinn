using System.Collections;
using UnityEngine;

public class EnemyDamage : MonoBehaviour
{
    [SerializeField] private Material[] Matts = new Material[2];

    private MeshRenderer meshRenderer;

    private bool IsDamage = false;

    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = Matts[0];
    }

    private IEnumerator GetDamage_co()
    {
        IsDamage = true;

        // 피격 효과
        meshRenderer.material = Matts[1];

        yield return new WaitForSeconds(0.2f);

        // 원래 색상
        meshRenderer.material = Matts[0];

        // 다음 피격까지 대기
        yield return new WaitForSeconds(0.5f);

        IsDamage = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Damage Range") && !IsDamage)
        {
            StartCoroutine(GetDamage_co());
        }
    }
}