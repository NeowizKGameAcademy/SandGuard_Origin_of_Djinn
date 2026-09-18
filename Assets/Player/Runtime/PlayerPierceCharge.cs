using System;
using UnityEngine;
using UnityEngine.Events;

namespace SandGuard.Player
{
    /// <summary>
    /// 관통탄(스킬트리 액티브 attack.pierce)의 충전. 스킬 키를 탭하면 기본 빔, <see cref="holdToCharge"/>보다 길게 누르면 손에 마나를 모으기 시작하고
    /// <see cref="chargeTime"/>에 만충, 키를 떼는 순간 충전량(0~1)으로 <see cref="PlayerBasicAttack.TrySkillPierce(float)"/>를 쏜다(피해·굵기·사거리·밀어내기는 거기 인스펙터).
    /// 충전 중에도 이동할 수 있고 카메라 정면을 본다. 입력이 막히거나(일시정지·창) 죽으면 충전이 취소된다.
    /// 어느 키가 관통탄인지는 스킬트리 실행기(PlayerSkillTreeExecutor)가 <see cref="BeginHold"/>/<see cref="EndHold"/>로 알려 준다.
    /// 연출·소리는 <see cref="onChargeStarted"/>·<see cref="onCharging"/>·<see cref="onChargeCancelled"/>·<see cref="onFired"/>(UnityEvent)와 같은 이름의 C# 이벤트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(205)]
    public sealed class PlayerPierceCharge : MonoBehaviour
    {
        public PlayerInputReader input;
        public PlayerBasicAttack attack;
        public PlayerMotor motor;
        [Tooltip("IManaWallet. 비우면 같은 오브젝트에서 찾는다")] public MonoBehaviour manaSource;
        [Header("비용")]
        [Min(0)] public int manaCost = 8;
        [Min(0), Tooltip("만충 시 추가 마나(충전량에 비례)")] public int fullChargeExtraMana = 8;
        [Min(0f)] public float cooldown = 2f;
        [Header("충전")]
        [Min(0f), Tooltip("이보다 짧게 누르면 탭(기본 빔). 넘기면 충전 시작")] public float holdToCharge = 0.15f;
        [Min(0.05f), Tooltip("충전 시작부터 만충까지")] public float chargeTime = 0.9f;
        [Tooltip("충전 중 카메라 정면을 보게 한다")] public bool faceCameraWhileCharging = true;
        [Header("연출 훅 (인스펙터 연결용)")]
        public UnityEvent onChargeStarted = new UnityEvent();
        [Tooltip("충전 중 매 프레임 (충전량 0~1)")] public UnityEvent<float> onCharging = new UnityEvent<float>();
        public UnityEvent onChargeCancelled = new UnityEvent();
        [Tooltip("발사 (충전량 0~1). 탭도 0으로 온다")] public UnityEvent<float> onFired = new UnityEvent<float>();
        /// <summary>스킬트리 게이트. null이면 항상 허용.</summary>
        public Func<bool> Allowed;
        public event Action ChargeStarted;
        public event Action<float> Charging;
        public event Action ChargeCancelled;
        /// <summary>(충전량 0~1). 실제로 빔이 나간 뒤.</summary>
        public event Action<float> Fired;

        public bool IsHolding => heldSlot >= 0;
        public int HeldSlot => heldSlot;
        public bool IsCharging { get; private set; }
        /// <summary>충전량 0~1. 충전 중이 아니면 0.</summary>
        public float Charge { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public float LastCharge { get; private set; }
        public int FireCount { get; private set; }
        int heldSlot = -1;
        float holdStart, readyAt;
        IManaWallet Mana => (manaSource as IManaWallet) ?? (motor != null ? motor.manaSource as IManaWallet : null);
        bool Usable => (Allowed == null || Allowed()) && attack != null && attack.CanFire;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (attack == null) attack = GetComponent<PlayerBasicAttack>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (manaSource == null) manaSource = GetComponent<IManaWallet>() as MonoBehaviour;
        }

        /// <summary>HUD 켜짐 표시용: 지금 쏠 수 있는가(쿨다운·마나·생존).</summary>
        public bool CanFire => Usable && CooldownRemaining <= 0f && (Mana?.CurrentMana ?? 0) >= manaCost;

        /// <summary>키를 눌렀다. 쿨다운 중이거나 못 쓰는 상태면 false를 돌려주고 아무 일도 하지 않는다.</summary>
        public bool BeginHold(int slot)
        {
            if (IsHolding || !Usable || CooldownRemaining > 0f) return false;
            heldSlot = slot; holdStart = Time.time; IsCharging = false; Charge = 0f;
            return true;
        }

        /// <summary>키를 뗐다. 누르고 있던 키가 아니면 무시. 탭이면 충전 0으로, 충전 중이면 그 충전량으로 쏜다.</summary>
        public ActionResult EndHold(int slot)
        {
            if (heldSlot != slot) return ActionResult.Fail(ActionFailure.InvalidRequest);
            float charge = IsCharging ? Charge : 0f;
            bool wasCharging = IsCharging;
            ResetHold();
            var result = Fire(charge);
            if (!result.Succeeded && wasCharging) { ChargeCancelled?.Invoke(); onChargeCancelled.Invoke(); }
            return result;
        }

        /// <summary>충전을 버린다(마나는 아직 쓰지 않았으므로 환불 없음).</summary>
        public void Cancel()
        {
            if (!IsHolding) return;
            bool was = IsCharging;
            ResetHold();
            if (was) { ChargeCancelled?.Invoke(); onChargeCancelled.Invoke(); }
        }

        void ResetHold()
        {
            heldSlot = -1;
            if (IsCharging && attack != null && attack.visuals != null) attack.visuals.SetPierceCharging(false); // 발사면 PlayPierceFire가 이어받고, 취소면 자세를 내린다
            IsCharging = false; Charge = 0f;
        }

        /// <summary>충전량으로 바로 쏜다. 마나는 기본 + 추가×충전량, 부족하면 실패.</summary>
        public ActionResult Fire(float charge)
        {
            charge = Mathf.Clamp01(charge);
            if (Allowed != null && !Allowed()) return ActionResult.Fail(ActionFailure.Locked);
            if (attack == null || !attack.CanFire)
                return ActionResult.Fail((attack != null ? attack.lifeSource as ILifeState : null)?.State is LifeState state && state != LifeState.Alive
                    ? ActionFailure.NotAlive : ActionFailure.InvalidRequest);
            if (CooldownRemaining > 0f) return ActionResult.Fail(ActionFailure.Cooldown);
            var mana = Mana;
            if (mana == null) return ActionResult.Fail(ActionFailure.NotFound);
            int cost = manaCost + Mathf.RoundToInt(fullChargeExtraMana * charge);
            if (mana.CurrentMana < cost || !mana.TrySpend(cost)) return ActionResult.Fail(ActionFailure.InsufficientMana);
            if (!attack.TrySkillPierce(charge)) { mana.Gain(cost); return ActionResult.Fail(ActionFailure.InvalidRequest); }
            readyAt = Time.time + cooldown;
            LastCharge = charge; FireCount++;
            Fired?.Invoke(charge);
            onFired.Invoke(charge);
            return ActionResult.Success();
        }

        void Update()
        {
            if (!IsHolding) return;
            if (input != null && !input.SkillHeld(heldSlot))
            {
                // 입력이 막힌 채 뗐거나(일시정지·창) 릴리즈 이벤트를 놓쳤다. 입력이 살아 있으면 뗀 것으로, 아니면 취소로 본다.
                if (input.AcceptsInput) EndHold(heldSlot); else Cancel();
                return;
            }
            if (!Usable) { Cancel(); return; }
            float held = Time.time - holdStart;
            if (!IsCharging)
            {
                if (held < holdToCharge) return;
                IsCharging = true; Charge = 0f;
                if (attack != null && attack.visuals != null) attack.visuals.SetPierceCharging(true); // 두 손을 모으는 자세
                ChargeStarted?.Invoke(); onChargeStarted.Invoke();
            }
            Charge = Mathf.Clamp01((held - holdToCharge) / chargeTime);
            Charging?.Invoke(Charge); onCharging.Invoke(Charge);
            if (faceCameraWhileCharging && motor != null) motor.FaceCamera();
        }

        /// <summary>부활 등으로 자원을 회복할 때 쿨다운을 지운다.</summary>
        public void ResetCooldown() => readyAt = 0f;

        void OnDisable() => Cancel();
    }
}
