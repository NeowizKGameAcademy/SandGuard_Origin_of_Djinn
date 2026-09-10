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

        UpdateSlowState();

        yield return new WaitForSeconds(0.5f);

        IsDamage = false;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out TowerRange range))
            return;

        GameObject tower = range.OwnerTower;

        // Detect Range 진입
        if (other.CompareTag("Detect Range"))
        {
            detectedTowers.Add(tower);

            Debug.Log($"Detect Enter : {tower.name}");
        }
    }


    private void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent(out TowerRange range))
            return;

        GameObject tower = range.OwnerTower;


        // ================================
        // Effect Range
        // ================================

        if (other.CompareTag("Effect Range"))
        {
            // 반드시 "이 Effect Range의 주인 타워"가
            // Enemy를 Detect하고 있을 때만 적용
            if (detectedTowers.Contains(tower))
            {
                effectTowers.Add(tower);
            }
            else
            {
                effectTowers.Remove(tower);
            }

            UpdateSlowState();

            return;
        }


        // ================================
        // Damage Range
        // ================================

        if (other.CompareTag("Damage Range"))
        {
            // 이 Damage Range의 주인 타워가
            // Detect 중인지 확인
            if (!detectedTowers.Contains(tower))
                return;

            if (!IsDamage)
            {
                StartCoroutine(GetDamage_co());
            }
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out TowerRange range))
            return;

        GameObject tower = range.OwnerTower;


        // Effect Range 이탈
        if (other.CompareTag("Effect Range"))
        {
            effectTowers.Remove(tower);

            Debug.Log($"Effect Exit : {tower.name}");

            UpdateSlowState();

            return;
        }


        // Detect Range 이탈
        if (other.CompareTag("Detect Range"))
        {
            detectedTowers.Remove(tower);

            // 이 Detect Range와 같은 타워가 주는 Effect만 제거
            effectTowers.Remove(tower);

            Debug.Log($"Detect Exit : {tower.name}");

            UpdateSlowState();

            return;
        }
        0

        // Damage Range 이탈
        if (other.CompareTag("Damage Range"))
        {
            // 아무것도 하지 않음
            // Damage Range를 나가는 것이 Slow에 영향을 주면 안 됨

            Debug.Log($"Damage Exit : {tower.name}");
        }
    }


    private void UpdateSlowState()
    {
        IsSlowed = effectTowers.Count > 0;

        if (IsSlowed)
        {
            meshRenderer.material.color =
                Color.Lerp(originalColor, Color.blue, 0.5f);
        }
        else
        {
            meshRenderer.material.color = originalColor;
        }
    }
}