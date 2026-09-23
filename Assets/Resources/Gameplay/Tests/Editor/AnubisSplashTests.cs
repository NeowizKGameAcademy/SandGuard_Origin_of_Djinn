using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>스플래시가 맞혔는지 기록하는 대역. 조준점은 발 위 0.5m라 높이 규칙에 걸리지 않는다.</summary>
public sealed class AnubisSplashTarget : MonoBehaviour, ICombatTarget, IDamageable
{
    public Guid EntityId { get; } = Guid.NewGuid();
    public string Faction = "Enemy";
    string ICombatTarget.FactionId => Faction;
    public CombatTargetKind Kind => CombatTargetKind.Enemy;
    public Vector3 HitPosition => transform.position + Vector3.up * 0.5f;
    public bool IsTargetable => isActiveAndEnabled;
    public IDamageable DamageReceiver => this;
    public ILifeState LifeState => null;
    public float Taken;
    public int Hits;
    public DamageResult TakeDamage(DamageInfo damage)
    {
        Taken += damage.Amount;
        Hits++;
        return DamageResult.Applied(damage.Amount);
    }
}

/// <summary>
/// 아누비스 일반 공격의 스플래시. 주 대상을 맞힌 지점에서 반경·전방 각도·높이·진영 규칙대로 번지는지 본다.
/// 타워 코드는 Assembly-CSharp에 있어 어셈블리 참조가 안 되므로 <see cref="MinionCombatRecoveryTests"/>와 같이 리플렉션으로 부른다.
/// </summary>
public sealed class AnubisSplashTests
{
    // 다른 테스트가 남긴 콜라이더와 겹치지 않도록 멀리 떨어진 곳에서 잰다.
    private static readonly Vector3 Origin = new Vector3(14000f, 1000f, 14000f);
    private const float Attack = 30f, Ratio = 0.5f, Radius = 2.5f;

    private readonly List<GameObject> objects = new List<GameObject>();
    private object config;
    private Component controller;
    private Transform anubis;

    private static Type Runtime(string name) => Type.GetType("Tower." + name + ", Assembly-CSharp", true);
    private static void PrivateCall(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    private static void SetPrivate(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private static object GetPrivate(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

    [SetUp]
    public void Setup()
    {
        // 타워: 아누비스는 설치된 타워의 TowerStatus에서 수치를 읽는다.
        var tower = Track("tower", Vector3.zero);
        var status = tower.AddComponent(Runtime("TowerStatus"));
        config = Activator.CreateInstance(Runtime("AnubisTowerConfig"));
        SetPrivate(config, "Attack", Attack);
        SetPrivate(config, "SplashRadius", Radius);
        SetPrivate(config, "SplashRatio", Ratio);
        SetPrivate(config, "SplashAngle", 120f);
        SetPrivate(config, "SplashMask", (LayerMask)~0);
        SetPrivate(status, "anubisConfig", config);

        // 아누비스 본체: +Z를 바라본다. 외형·선택기·스킬은 이 규칙과 무관하므로 붙이지 않는다.
        var unit = Track("anubis", Vector3.zero);
        unit.transform.SetParent(tower.transform, true);
        unit.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        controller = unit.AddComponent(Runtime("AnubisController"));
        PrivateCall(controller, "Awake");
        anubis = unit.transform;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        objects.Clear();
    }

    private GameObject Track(string name, Vector3 offset)
    {
        var obj = new GameObject(name);
        obj.transform.position = Origin + offset;
        objects.Add(obj);
        return obj;
    }

    /// <summary>판정에 걸릴 수 있는 적. 스플래시는 콜라이더를 겹쳐 찾으므로 몸을 하나 준다.</summary>
    private AnubisSplashTarget Enemy(string name, Vector3 offset, string faction = "Enemy")
    {
        var obj = Track(name, offset);
        obj.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;
        var target = obj.AddComponent<AnubisSplashTarget>();
        target.Faction = faction;
        return target;
    }

    /// <summary>주 대상을 때린다. 애니메이션 타격 시점에 아누비스가 하는 일과 같다.</summary>
    private void Strike(AnubisSplashTarget primary)
    {
        var sequence = GetPrivate(controller, "attackSequence");
        sequence.GetType().GetMethod("Begin")
            .Invoke(sequence, new object[] { primary, "Mutant Swiping", 0.45f, false });
        Physics.SyncTransforms();
        PrivateCall(controller, "ApplyAttackImpact");
    }

    [Test]
    public void SplashHitsNeighboursOnceForHalfDamageAndSparesTheRest()
    {
        // 주 대상은 사거리(stopDistance 1.5) 안. 스플래시 중심은 이 대상의 조준점이다.
        var primary = Enemy("primary", Vector3.forward);
        var inFront = Enemy("in front", new Vector3(1f, 0f, 2f));     // 중심에서 1.5m, 전방 27도
        var outsideArc = Enemy("outside arc", new Vector3(2f, 0f, 1f)); // 중심에서 2m지만 전방 63도
        var behind = Enemy("behind", -Vector3.forward);                 // 중심에서 2m지만 등 뒤
        var tooFar = Enemy("too far", Vector3.forward * 4f);            // 전방이지만 중심에서 3m
        var upstairs = Enemy("upstairs", new Vector3(0f, 2.2f, 1f));    // 반경 안이지만 닿지 않는 높이
        var ally = Enemy("ally", Vector3.forward * 2f, "Ally");         // 반경·각도 안이지만 같은 편

        Strike(primary);

        Assert.AreEqual(Attack, primary.Taken, 0.001f, "주 대상은 온전한 피해만 받는다(스플래시가 겹치지 않는다).");
        Assert.AreEqual(1, primary.Hits, "주 대상을 두 번 때리지 않는다.");
        Assert.AreEqual(Attack * Ratio, inFront.Taken, 0.001f, "전방 각도·반경 안의 적은 절반을 받는다.");
        Assert.AreEqual(1, inFront.Hits, "한 번 휘두르면 한 번만 맞는다.");
        Assert.Zero(outsideArc.Hits, "전방 120도를 벗어난 적에게는 번지지 않는다.");
        Assert.Zero(behind.Hits, "등 뒤로는 번지지 않는다.");
        Assert.Zero(tooFar.Hits, "반경을 벗어난 적에게는 번지지 않는다.");
        Assert.Zero(upstairs.Hits, "닿지 않는 높이에는 번지지 않는다.");
        Assert.Zero(ally.Hits, "같은 편에게는 번지지 않는다.");
    }

    [Test]
    public void FullCircleAngleSplashesBehindToo()
    {
        SetPrivate(config, "SplashAngle", 360f);
        var primary = Enemy("primary", Vector3.forward);
        var behind = Enemy("behind", -Vector3.forward);

        Strike(primary);

        Assert.AreEqual(Attack * Ratio, behind.Taken, 0.001f, "360도면 등 뒤도 번진다.");
    }

    [Test]
    public void ZeroRadiusKeepsTheAttackSingleTarget()
    {
        SetPrivate(config, "SplashRadius", 0f);
        var primary = Enemy("primary", Vector3.forward);
        var neighbour = Enemy("neighbour", new Vector3(1f, 0f, 2f));

        Strike(primary);

        Assert.AreEqual(Attack, primary.Taken, 0.001f, "주 대상은 그대로 맞는다.");
        Assert.Zero(neighbour.Hits, "반경이 0이면 스플래시가 없다.");
    }

    [Test]
    public void OneBodyIsStruckOnceEvenWithSeveralColliders()
    {
        var primary = Enemy("primary", Vector3.forward);
        var neighbour = Enemy("neighbour", new Vector3(1f, 0f, 2f));
        // 방패병처럼 몸과 방패가 따로 겹치는 경우. 같은 EntityId라 한 번만 맞아야 한다.
        var shield = new GameObject("shield");
        shield.transform.SetParent(neighbour.transform, false);
        shield.transform.localPosition = new Vector3(0f, 0f, -0.3f);
        shield.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;

        Strike(primary);

        Assert.AreEqual(1, neighbour.Hits, "충돌체가 여러 개여도 한 대상은 한 번이다.");
        Assert.AreEqual(Attack * Ratio, neighbour.Taken, 0.001f);
    }
}
