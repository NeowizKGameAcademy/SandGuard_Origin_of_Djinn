using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tower
{
    public class TowerEmit : MonoBehaviour
    {
        [SerializeField] private TargetDetector detector;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] Material[] materials = new Material[2];
        [SerializeField] private float ChangeTime = 0.5f;

        private Material inactiveMaterial;
        private Material activeMaterial;
        private Material currentMaterial;

        private bool CurrentState;

        private Coroutine ChangeCoroutine;

        private void Awake()
        {
            inactiveMaterial = materials[0];
            activeMaterial = materials[1];

            currentMaterial = new Material(inactiveMaterial);

            meshRenderer.material = currentMaterial;

            CurrentState = false;
        }

        private void Update()
        {
            bool newState = detector.IsDetecting;

            if (CurrentState == newState)
                return;

            CurrentState = newState;

            ChangeMaterial(CurrentState);
        }

        private void ChangeMaterial(bool active)
        {
            Material target = active ? activeMaterial : inactiveMaterial;

            if (ChangeCoroutine != null)
                StopCoroutine(ChangeCoroutine);

            ChangeCoroutine = StartCoroutine(ChangeMaterial_co(target));
        }

        private IEnumerator ChangeMaterial_co(Material target)
        {
            Material start = new Material(currentMaterial);

            float time = 0f;

            while (time < ChangeTime)
            {
                time += Time.deltaTime;
                float t = time / ChangeTime;

                currentMaterial.Lerp(start, target, t);

                yield return null;
            }

            currentMaterial.Lerp(start, target, 1f);

            Destroy(start);

            ChangeCoroutine = null;
        }
    }
}
