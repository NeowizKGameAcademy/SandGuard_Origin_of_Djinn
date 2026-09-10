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
                    }
                    ConnectCrosshair(root);
                    ConnectRespawner(root);
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
