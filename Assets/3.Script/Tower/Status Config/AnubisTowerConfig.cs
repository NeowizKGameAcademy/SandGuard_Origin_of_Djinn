using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Tower
{
    [System.Serializable]
    public class AnubisTowerConfig
    {
        [Header("Spawn Point")]
        [SerializeField] private Transform SpawnPoint;

        [Header("Creature Status")]
        [SerializeField] private float MaxHP = 1000f;
        [SerializeField] private float Attack = 30f;
        [SerializeField] private float AttackInterval = 1.5f;
        [SerializeField] private float MoveSpeed = 5f;
        [SerializeField] private float RotateSpeed = 5f;
        [SerializeField] private float RespawnCooldown = 15f;
        [SerializeField] private float KnockBackCooldown = 5f;

        //Properties
        public Transform spawnPoint => SpawnPoint;

        public float maxHP => MaxHP;
        public float attack => Attack;
        public float attackInterval => AttackInterval;
        public float moveSpeed => MoveSpeed;
        public float rotateSpeed => RotateSpeed;
        public float respawnCooldown => RespawnCooldown;
        public float knockBackCooldown => KnockBackCooldown;
    }
}