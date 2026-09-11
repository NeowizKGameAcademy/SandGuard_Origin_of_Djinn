using UnityEditor;
using UnityEngine;

namespace SandGuard.Player.Editor
{
    /// <summary>
    /// 조작감 튜닝값을 이미 생성된 프리팹에 적용한다. 새 필드(가감속 분리, 가변 점프, 코요테·버퍼, 접지 스냅, 카메라 스무딩)는
    /// 코드 기본값으로 들어오므로, 여기서는 프리팹에 이미 저장된 옛 값만 새 값으로 바꾼다. 여러 번 실행해도 안전하다.
    /// </summary>
    public static class PlayerFeelTuning
    {
        const string PlayerPrefabPath = "Assets/Player/Generated/Player.prefab";
        const string BoltPrefabPath = "Assets/Player/Generated/PlayerBolt.prefab";
        public const float CameraDistance = 3.5f;
        public const float CameraShoulderOffset = 0.9f;

        /// <summary>플레이어 루트에 상승 기류(③) 능력을 붙인다. 해금은 UpdraftEffect가 켠다.</summary>
        public static PlayerUpdraft ConnectUpdraft(GameObject root)
        {
            var updraft = root.GetComponent<PlayerUpdraft>();
            if (updraft == null) updraft = root.AddComponent<PlayerUpdraft>();
            updraft.input = root.GetComponent<PlayerInputReader>();
            updraft.motor = root.GetComponent<PlayerMotor>();
            updraft.manaSource = root.GetComponent<PlayerManaWallet>();
            var visuals = root.GetComponent<PlayerVisuals>(); if (visuals != null) visuals.updraft = updraft;
            var feel = root.GetComponent<PlayerUpdraftCameraFeel>();
            if (feel == null) feel = root.AddComponent<PlayerUpdraftCameraFeel>();
            feel.updraft = updraft; feel.rig = root.GetComponentInChildren<PlayerCameraRig>(true);
            return updraft;
        }

        /// <summary>플레이어 루트에 대시 카메라 연출(시야 확대)을 붙인다. 이미 있으면 참조만 다시 채운다.</summary>
        public static PlayerDashCameraFeel ConnectDashCameraFeel(GameObject root)
        {
            var feel = root.GetComponent<PlayerDashCameraFeel>();
            if (feel == null) feel = root.AddComponent<PlayerDashCameraFeel>();
            feel.motor = root.GetComponent<PlayerMotor>();
            feel.rig = root.GetComponentInChildren<PlayerCameraRig>(true);
            return feel;
        }

        /// <summary>플레이어 루트에 Q/E/R 스킬 시전 컴포넌트를 붙인다. 이미 있으면 참조만 다시 채운다.</summary>
        public static PlayerSkillCaster ConnectSkillCaster(GameObject root)
        {
            var caster = root.GetComponent<PlayerSkillCaster>();
            if (caster == null) caster = root.AddComponent<PlayerSkillCaster>();
            caster.input = root.GetComponent<PlayerInputReader>();
            caster.attack = root.GetComponent<PlayerBasicAttack>();
            caster.aimer = root.GetComponent<PlayerAimer>();
            caster.stats = root.GetComponent<PlayerStats>();
            caster.motor = root.GetComponent<PlayerMotor>();
            caster.manaSource = root.GetComponent<PlayerManaWallet>();
            return caster;
        }

        /// <summary>플레이어 루트에 효과(스킬·버프) 목록 컴포넌트를 붙인다.</summary>
        public static PlayerEffects ConnectEffects(GameObject root)
        {
            var effects = root.GetComponent<PlayerEffects>();
            if (effects == null) effects = root.AddComponent<PlayerEffects>();
            var attack = root.GetComponent<PlayerBasicAttack>();
            if (attack != null && attack.manaSource == null) attack.manaSource = root.GetComponent<PlayerManaWallet>();
            return effects;
        }

        /// <summary>플레이어 루트에 스탯 수정자 층을 붙이고 모터·공격·체력이 그것을 읽게 연결한다.</summary>
        public static PlayerStats ConnectStats(GameObject root)
        {
            var stats = root.GetComponent<PlayerStats>();
            if (stats == null) stats = root.AddComponent<PlayerStats>();
            var motor = root.GetComponent<PlayerMotor>(); if (motor != null) motor.stats = stats;
            var attack = root.GetComponent<PlayerBasicAttack>(); if (attack != null) attack.stats = stats;
            var health = root.GetComponent<PlayerHealth>(); if (health != null) health.stats = stats;
            return stats;
        }

        /// <summary>플레이어 루트에 스폰 지점 부활 컴포넌트를 붙인다. 이미 있으면 참조만 다시 채운다.</summary>
        public static PlayerRespawner ConnectRespawner(GameObject root)
        {
            var respawner = root.GetComponent<PlayerRespawner>();
            if (respawner == null) respawner = root.AddComponent<PlayerRespawner>();
            respawner.motor = root.GetComponent<PlayerMotor>();
            respawner.cameraRig = root.GetComponentInChildren<PlayerCameraRig>(true);
            respawner.attack = root.GetComponent<PlayerBasicAttack>();
            var health = root.GetComponent<PlayerHealth>();
            if (health != null) respawner.lifeSource = health;
            return respawner;
        }

        /// <summary>플레이어 루트에 화면 중앙 조준점을 붙인다. 이미 있으면 참조만 다시 채운다.</summary>
        public static PlayerCrosshair ConnectCrosshair(GameObject root)
        {
            var crosshair = root.GetComponent<PlayerCrosshair>();
            if (crosshair == null) crosshair = root.AddComponent<PlayerCrosshair>();
            crosshair.input = root.GetComponent<PlayerInputReader>();
            crosshair.motor = root.GetComponent<PlayerMotor>();
            var visuals = root.GetComponent<PlayerVisuals>();
            MonoBehaviour life = visuals != null && visuals.healthSource != null ? visuals.healthSource : root.GetComponent<PlayerHealth>();
            if (life != null) crosshair.lifeSource = life;
            return crosshair;
        }

        [MenuItem("SandGuard/Player/Apply Feel Tuning")]
        public static void Apply()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
                try
                {
                    var controller = root.GetComponent<CharacterController>();
                    if (controller != null) controller.stepOffset = 0.45f;
                    var motor = root.GetComponent<PlayerMotor>();
                    if (motor != null) { motor.dashCooldown = 1f; motor.alwaysFaceCamera = false; }
                    var attack = root.GetComponent<PlayerBasicAttack>();
                    if (attack != null && motor != null) attack.motor = motor;
                    var rig = root.GetComponentInChildren<PlayerCameraRig>(true);
                    if (rig != null)
                    {
                        // 포트나이트식 오른쪽 어깨 너머 시점: 몸이 화면 왼쪽 1/3에 오고 중앙 조준점을 가리지 않는다.
                        rig.distance = CameraDistance; rig.shoulderOffset = CameraShoulderOffset;
                        rig.mouseSensitivity = 0.18f; rig.stickDegreesPerSecond = 220f;
                        rig.collisionRecoverTime = 0.12f; rig.verticalSmoothTime = 0.08f; rig.pitchLift = 0.5f;
                        // 대시(초당 13m) 때 피벗은 늘 maxFollowLag에 걸리므로 이 값이 곧 "몸이 먼저 튀어나가는" 뒤처짐의 크기다. 0.8은 딱딱하게 끌리는 느낌이라 줄인다.
                        rig.followSmoothTime = 0.08f; rig.maxFollowLag = 0.5f;
                    }
                    ConnectCrosshair(root);
                    ConnectRespawner(root);
                    ConnectStats(root);
                    ConnectEffects(root);
                    ConnectUpdraft(root);
                    ConnectDashCameraFeel(root);
                    ConnectSkillCaster(root);
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var bolt = AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefabPath);
            if (bolt != null)
            {
                var root = PrefabUtility.LoadPrefabContents(BoltPrefabPath);
                try
                {
                    var projectile = root.GetComponent<PlayerProjectile>();
                    if (projectile != null) projectile.speed = 45f;
                    PrefabUtility.SaveAsPrefabAsset(root, BoltPrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_FEEL_TUNING_APPLIED");
        }
    }
}
