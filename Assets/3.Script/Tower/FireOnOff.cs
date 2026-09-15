using System.Collections;
using UnityEngine;

public class FireOnOff : MonoBehaviour
{
    [SerializeField] private DetectRange detectRange;

    [Header("Particle")]
    [SerializeField] private ParticleSystem Flames;
    [SerializeField] private ParticleSystem Cubes;
    [SerializeField] private ParticleSystem Embers;

    [Header("Light")]
    [SerializeField] private Light towerLight;

    [Header("Value")]
    private float[] OnValues = { 150f, 40f, 100f, 4f };
    private float[] OffValues = { 0f, 0f, 0f, 0f };

    [SerializeField] private float ChangeTime = 0.5f;

    private bool CurrentState;

    private Coroutine ChangeCoroutine;

    private void Update()
    {
        bool newState = detectRange.IsDetecting;

        if (CurrentState == newState)
            return;

        CurrentState = newState;

        Flame(CurrentState);
    }

    private void Flame(bool active)
    {
        if (ChangeCoroutine != null)
            StopCoroutine(ChangeCoroutine);

        float[] target = active ? OnValues : OffValues;

        ChangeCoroutine = StartCoroutine(Flame_co(target));
    }

    private IEnumerator Flame_co(float[] target)
    {
        ParticleSystem.MainModule flamesMain = Flames.main;
        ParticleSystem.MainModule cubesMain = Cubes.main;
        ParticleSystem.MainModule embersMain = Embers.main;

        float[] start =
        {
            flamesMain.maxParticles,
            cubesMain.maxParticles,
            embersMain.maxParticles,
            towerLight.intensity
        };

        float time = 0f;

        while (time < ChangeTime)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / ChangeTime);

            flamesMain.maxParticles =
                Mathf.RoundToInt(Mathf.Lerp(start[0], target[0], t));

            cubesMain.maxParticles =
                Mathf.RoundToInt(Mathf.Lerp(start[1], target[1], t));

            embersMain.maxParticles =
                Mathf.RoundToInt(Mathf.Lerp(start[2], target[2], t));

            towerLight.intensity =
                Mathf.Lerp(start[3], target[3], t);

            yield return null;
        }

        flamesMain.maxParticles = Mathf.RoundToInt(target[0]);
        cubesMain.maxParticles = Mathf.RoundToInt(target[1]);
        embersMain.maxParticles = Mathf.RoundToInt(target[2]);
        towerLight.intensity = target[3];

        ChangeCoroutine = null;
    }
}