using System.Collections.Generic;
using DesertTower.VFX;
using SandGuard.Player;
using UnityEngine;

namespace DesertTower.LevelIntegration
{
    /// <summary>Shows the core's destruction from its first frame and holds the shot behind the result UI.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1100)]
    public sealed class CoreDefeatCamera : MonoBehaviour
    {
        [Min(.1f)] public float holdSeconds = 2.4f;
        [Range(25f, 80f)] public float fieldOfView = 50f;
        public bool IsActive { get; private set; }
        public bool IsFinished => IsActive && elapsed >= holdSeconds;
        public Vector3 FocusPoint { get; private set; }

        readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        PlayerCameraRig rig;
        PlayerInputReader input;
        Camera view;
        Vector3 gameplayPosition, direction;
        Quaternion gameplayRotation;
        float gameplayFov, distance, elapsed;
        bool previousGameplayEnabled;

        public bool Begin(CoreReceiver core)
        {
            if (IsActive) return true;
            if (!isActiveAndEnabled || !core) return false;
            rig = null;
            foreach (var candidate in FindObjectsByType<PlayerCameraRig>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == core.gameObject.scene && candidate.isActiveAndEnabled)
                { rig = candidate; break; }
            view = rig ? rig.GetComponent<Camera>() : null;
            if (!view || !view.isActiveAndEnabled) return false;

            // Source renderers may already be hidden by the destruction VFX. Their bounds
            // still describe the model; exclude particles and the expanding explosion.
            var destruction = core.GetComponentInChildren<VfxDestructionOnDeath>(true);
            Transform model = destruction ? (destruction.Target ? destruction.Target : destruction.transform) : core.transform;
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var bounds = new Bounds(core.transform.position + Vector3.up * 2f, Vector3.one * 6f);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            FocusPoint = bounds.center;
            float halfFov = fieldOfView * .5f * Mathf.Deg2Rad;
            float limitingFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * view.aspect));
            distance = Mathf.Max(12f, bounds.extents.magnitude * 1.35f / Mathf.Sin(limitingFov));
            direction = new Vector3(1f, .65f, -1f).normalized;
            SaveGameplayPose();
            input = rig.input;
            if (input) { previousGameplayEnabled = input.GameplayEnabled; input.GameplayEnabled = false; }
            var resultCanvas = GetComponentInParent<Canvas>();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas != resultCanvas && canvas.gameObject.scene == core.gameObject.scene
                    && canvas.renderMode != RenderMode.WorldSpace && canvas.enabled)
                { hiddenCanvases.Add(canvas); canvas.enabled = false; }
            elapsed = 0f;
            IsActive = true;
            ApplyPose();
            return true;
        }

        void LateUpdate()
        {
            if (!IsActive) return;
            if (!view || !rig || !rig.isActiveAndEnabled) { ResetView(); return; }
            SaveGameplayPose();
            // Keep the destruction and this shot on the same clock. Do not pause the VFX
            // to lock player controls; the result screen pauses after the debris lands.
            elapsed += Time.deltaTime;
            ApplyPose();
        }

        void SaveGameplayPose()
        {
            gameplayPosition = view.transform.position;
            gameplayRotation = view.transform.rotation;
            gameplayFov = view.fieldOfView;
        }

        void ApplyPose()
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.1f, holdSeconds));
            view.transform.SetPositionAndRotation(FocusPoint + direction * distance * Mathf.Lerp(1f, .92f, t),
                Quaternion.LookRotation(-direction));
            view.fieldOfView = fieldOfView;
        }

        public void ResetView()
        {
            if (!IsActive) return;
            IsActive = false;
            if (view)
            {
                view.transform.SetPositionAndRotation(gameplayPosition, gameplayRotation);
                view.fieldOfView = gameplayFov;
            }
            if (input) input.GameplayEnabled = previousGameplayEnabled;
            foreach (var canvas in hiddenCanvases) if (canvas) canvas.enabled = true;
            hiddenCanvases.Clear();
        }

        void OnDisable() => ResetView();
    }
}
