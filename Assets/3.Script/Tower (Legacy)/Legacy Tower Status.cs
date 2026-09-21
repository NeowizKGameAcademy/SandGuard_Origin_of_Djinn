using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegacyTowerStatus : MonoBehaviour
{
    [Header("Basic Status")]
    [SerializeField] private float MaxHP = 200f;
    [SerializeField] private float CurrentHP;
    [SerializeField] private float DetectRange = 20f;

    [Header("Cobra Status")]
    [SerializeField] private float TickDamage = 10f;
    [SerializeField] private float TickInterval = 1f;

    [Header("Obelisk Status")]
    [SerializeField] private float SlowRatio = 0.3f;
    [SerializeField] private int ManaGain = 10;
    [SerializeField] private float GainTick = 1f;

    [Header("Coffin Status")]
    [SerializeField] private int CreatureNum = 7;
    [SerializeField] private float SummonDistance = 0.3f;

    //Properties
    public float maxHP => MaxHP;
    public float curHP => CurrentHP;
    public float detectRange => DetectRange;
    public float tickDamage => TickDamage;
    public float tickInterval => TickInterval;
    public float slowRatio => SlowRatio;
    public int manaGain => ManaGain;
    public float gainTick => GainTick;
    public int creatureNum => CreatureNum;
    public float summonDistance => SummonDistance;

    private void OnEnable()
    {
        CurrentHP = MaxHP;
    }

    private void Update()
    {
        DetectRange = Mathf.Clamp(DetectRange, 0f, 50f);
    }
}