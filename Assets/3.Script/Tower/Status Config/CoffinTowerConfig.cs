using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class CoffinTowerConfig
    {
        [Header("Summon")]
        [SerializeField] private int Num = 7;
        [SerializeField] private float Distance = 0.3f;

        [Header("Creature Status")]
        [SerializeField] private float MaxHP = 100f;
        [SerializeField] private float Attack = 10f;
        [SerializeField] private float AttackInterval = 1f;
        [SerializeField] private float MoveSpeed = 5f;
        [SerializeField] private float RotateSpeed = 0.5f;
        [SerializeField] private float RespawnCooldown = 5f;

        //Properties
        public int num => Num;
        public float distance => Distance;

        public float maxHP => MaxHP;
        public float attack => Attack;
        public float attackInterval => AttackInterval;
        public float moveSpeed => MoveSpeed;
        public float rotateSpeed => RotateSpeed;
        public float respawnCooldown => RespawnCooldown;
    }
}