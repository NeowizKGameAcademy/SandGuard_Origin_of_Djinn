using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnubisStatus : MonoBehaviour
{
    [Header("Basic Status")]
    [SerializeField] private float MaxHP = 200f;
    [SerializeField] private float CurrentHP;
    [SerializeField] private float MoveSpeed = 5f;
    [SerializeField] private float AttackPower = 30f;
    [SerializeField] private float RespawnCoolDown = 10f;

    private bool isInitialized;

    //Properties
    public float maxHp => MaxHP;
    public float currentHP => currentHP;
    public float moveSpeed => MoveSpeed;
    public float attackPower => AttackPower;
    public float respawnCoolDown => RespawnCoolDown;

    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        CurrentHP = MaxHP;

        isInitialized = true;
    }

    public void GetDamage(float damage)
    {
        CurrentHP -= damage;
    }

    public void GainHP(float gain)
    {
        CurrentHP += gain;
    }

    public void Despawn()
    {
        isInitialized = false;

        gameObject.SetActive(false);
    }
}
