using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SandGuard.Player;

public class CoreController : MonoBehaviour
{
    [SerializeField] private PlayerManaWallet Player;

    [Header("Range")]
    [SerializeField] private float Range = 25f;
    [SerializeField] private GameObject Range_Circle;

    [Header("Mana Gain")]
    [SerializeField] private int Gain_Amount = 10;
    [SerializeField] private float Gain_Interval = 1.5f;

    private Coroutine ReGainCoroutine;

    private void Awake()
    {
        Player = FindAnyObjectByType<PlayerManaWallet>();
    }

    private void Update()
    {
        ReGainMana();
        SetRange();
    }

    public void SetRange()
    {
        Material matt = Range_Circle.GetComponent<MeshRenderer>().material;

        Range_Circle.transform.localScale = Vector3.one * Range * 2;

        matt.SetFloat("_Outline_Width", 0.05f / Range);
        matt.SetFloat("_Hex_Tiling", Range * 0.08f);
    }

    private void ReGainMana()
    {
        if (Player == null)
            return;

        Vector3 playerPosition = Player.transform.position;

        float Distance = (playerPosition - transform.position).sqrMagnitude;

        bool IsInRange = Distance <= Range * Range;

        if (IsInRange)
        {
            if (ReGainCoroutine == null)
                ReGainCoroutine = StartCoroutine(ReGainMana_co());
        }
        else
        {
            if (ReGainCoroutine != null)
            {
                StopCoroutine(ReGainCoroutine);
                ReGainCoroutine = null;
            }
        }
    }

    private IEnumerator ReGainMana_co()
    {
        while (true)
        {
            Player.Gain(Gain_Amount);

            Debug.Log("Mana Gained");

            yield return new WaitForSeconds(Gain_Interval);
        }
    }
}
