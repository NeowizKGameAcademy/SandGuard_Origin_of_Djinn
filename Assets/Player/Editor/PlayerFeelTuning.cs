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
                        rig.distance = 6f; rig.shoulderOffset = 0.5f;
                        rig.mouseSensitivity = 0.18f; rig.stickDegreesPerSecond = 220f;
                        rig.collisionRecoverTime = 0.12f; rig.verticalSmoothTime = 0.08f; rig.pitchLift = 0.5f;
                    }
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
