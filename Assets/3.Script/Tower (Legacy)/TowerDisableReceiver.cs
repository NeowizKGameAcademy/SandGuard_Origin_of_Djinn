using System.Collections.Generic;
using DesertTower.VFX;
using UnityEngine;

// =====================================================================================
// [통합 추가 2026-09-14] 타워 정지 요청을 받는 새 컴포넌트입니다. 기존 타워 코드는 바꾸지 않습니다.
//
// 왜 필요한가
//   - 기획서: 보스(자히르)의 "철거 폭탄"은 주변 시설을 5초 동안 정지시킵니다.
//   - 폭탄(적 쪽 코드)은 타워 내부를 직접 건드리지 않고 "이 타워를 N초 정지해 달라"는 요청만 보냅니다
//     (CombatEffectSignals.TowerDisableRequested). 요청을 받아 실제로 멈추는 일은 타워 쪽이 맡아야 해서 추가했습니다.
//
// 무엇을 하나
//   - 요청의 대상 ID가 이 타워 본체의 전투 ID(ICombatTarget.EntityId, 본체의 체력 컴포넌트)와 같으면
//     그 시간 동안 조준·회전·공격 컴포넌트(FindEnemy, RotateTower, FirePointAim, RangeController)를 끄고
//     본체의 파티클(화염 등)을 멈춘다. 시간이 끝나면 원래대로 켠다. 정지 중 다시 요청이 오면 더 긴 쪽으로 늘린다.
//   - 정지 연출 프리팹(선택)을 본체 자식으로 띄우고, 본체 색을 어둡게 한다(VfxDisabledVisual).
//   - 본체가 파괴(비활성)되면 연출을 거두고 상태를 되돌린다.
//
// 본체(Tower (Cobra) 등)에 붙입니다. 끄는 컴포넌트 목록은 비워 두면 본체 아래에서 자동으로 찾습니다.
// =====================================================================================
public class TowerDisableReceiver : MonoBehaviour
{
    [Header("Stop While Disabled")]
    [Tooltip("정지 동안 끌 컴포넌트. 비우면 본체 아래의 FindEnemy / RotateTower / FirePointAim / RangeController")]
    [SerializeField] private Behaviour[] Stop_Behaviours = new Behaviour[0];

    [Header("VFX")]
    [Tooltip("정지 동안 본체에 띄울 연출(VFX_Facility_Disabled_Loop). 비우면 연출 없이 멈추기만 한다")]
    [SerializeField] private GameObject Disabled_Vfx;

    private ICombatTarget Owner;
    private float Disabled_Until = -1f;
    private bool Applied;
    private GameObject Vfx_Instance;
    private readonly List<Behaviour> Stopped = new();
    private readonly List<ParticleSystem> Paused_Particles = new();

    public bool IsDisabled => Applied;

    private void Awake()
    {
        Owner = GetComponentInParent<ICombatTarget>();

        if (Stop_Behaviours == null || Stop_Behaviours.Length == 0)
        {
            var Found = new List<Behaviour>();
            Found.AddRange(GetComponentsInChildren<FindEnemy>(true));
            Found.AddRange(GetComponentsInChildren<RotateTower>(true));
            Found.AddRange(GetComponentsInChildren<FirePointAim>(true));
            Found.AddRange(GetComponentsInChildren<RangeController>(true));
            Stop_Behaviours = Found.ToArray();
        }
    }

    private void OnEnable() => CombatEffectSignals.TowerDisableRequested += OnRequested;

    private void OnDisable()
    {
        CombatEffectSignals.TowerDisableRequested -= OnRequested;
        // 비활성화되는 중에는 자식의 부모를 바꿀 수 없어서, 연출 반납을 한 프레임 미룬다.
        Resume(true);
        Disabled_Until = -1f;
    }

    private void OnRequested(TowerDisableRequest Request)
    {
        if (Owner == null || Request.TargetEntityId != Owner.EntityId || !Owner.IsTargetable)
            return;

        Disabled_Until = Mathf.Max(Disabled_Until, Time.time + Request.Duration);

        if (!Applied)
            Stop();
    }

    private void Update()
    {
        if (Applied && Time.time >= Disabled_Until)
            Resume(false);
    }

    private void Stop()
    {
        Applied = true;
        Stopped.Clear();

        foreach (var Stoppable in Stop_Behaviours)
        {
            if (Stoppable != null && Stoppable.enabled)
            {
                Stoppable.enabled = false;
                Stopped.Add(Stoppable);
            }
        }

        Paused_Particles.Clear();

        foreach (var Particle in GetComponentsInChildren<ParticleSystem>())
        {
            if (Particle.isPlaying)
            {
                Particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                Paused_Particles.Add(Particle);
            }
        }

        if (Disabled_Vfx != null)
        {
            Vfx_Instance = PrefabPool.Spawn(Disabled_Vfx, transform.position, transform.rotation, transform);
            Vfx_Instance.transform.localPosition = Vector3.zero;
            Vfx_Instance.transform.localRotation = Quaternion.identity;

            var Visual = Vfx_Instance.GetComponent<VfxDisabledVisual>();
            if (Visual != null)
                Visual.BindTarget(transform);
        }
    }

    private void Resume(bool Defer_Release)
    {
        if (!Applied)
            return;

        Applied = false;

        foreach (var Stoppable in Stopped)
        {
            if (Stoppable != null)
                Stoppable.enabled = true;
        }

        Stopped.Clear();

        foreach (var Particle in Paused_Particles)
        {
            if (Particle != null)
                Particle.Play(false);
        }

        Paused_Particles.Clear();

        if (Vfx_Instance != null)
        {
            var Visual = Vfx_Instance.GetComponent<VfxDisabledVisual>();
            if (Visual != null)
                Visual.Restore();

            PrefabPool.Release(Vfx_Instance, Defer_Release ? 0.01f : 0f);
            Vfx_Instance = null;
        }
    }
}
