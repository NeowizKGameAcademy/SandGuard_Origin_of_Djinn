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
        [SerializeField] private float MaxHP = 4000f;
        [SerializeField] private float Attack = 50f;
        [SerializeField] private float AttackInterval = 3f;
        [SerializeField] private float MoveSpeed = 5f;
        [SerializeField] private float RotateSpeed = 5f;
        [SerializeField] private float RespawnCooldown = 10f;
        [SerializeField] private float KnockBackCooldown = 5f;

        [Header("Survival")]
        [Range(0f, 1f)] [SerializeField] private float DamageReduction = 0f;
        [Min(0f)] [SerializeField] private float HealPerKill = 0f;
        [Min(0f)] [SerializeField] private float MaxKillHealingPerSecond = 200f;

        [Header("Splash Damage")]
        [Min(0f)] [SerializeField] [Tooltip("주 대상을 맞힌 지점 기준 반경. 0이면 스플래시가 없다")]
        private float SplashRadius = 7f;
        [Range(0f, 1f)] [SerializeField] [Tooltip("주 대상 피해 대비 주변 피해 비율")]
        private float SplashRatio = 1f;
        [Range(0f, 360f)] [SerializeField] [Tooltip("전방 몇 도까지 번지는지. 120이면 바라보는 쪽 120도. 360이면 전방향")]
        private float SplashAngle = 180f;
        [SerializeField] [Tooltip("스플래시 판정에 쓰는 레이어")]
        private LayerMask SplashMask = ~0;

        //Properties
        public Transform spawnPoint => SpawnPoint;

        public float maxHP => MaxHP;
        public float attack => Attack;
        public float attackInterval => AttackInterval;
        public float moveSpeed => MoveSpeed;
        public float rotateSpeed => RotateSpeed;
        public float respawnCooldown => RespawnCooldown;
        public float knockBackCooldown => KnockBackCooldown;
        public float damageReduction => DamageReduction;
        public float healPerKill => HealPerKill;
        public float maxKillHealingPerSecond => MaxKillHealingPerSecond;
        public float splashRadius => SplashRadius;
        public float splashRatio => SplashRatio;
        public float splashAngle => SplashAngle;
        public LayerMask splashMask => SplashMask;
    }
}
