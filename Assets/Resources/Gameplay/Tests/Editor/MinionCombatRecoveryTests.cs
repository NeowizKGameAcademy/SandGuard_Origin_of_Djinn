using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public sealed class MinionCombatRecoveryTarget : MonoBehaviour, ICombatTarget, IDamageable
{
    public Guid EntityId { get; } = Guid.NewGuid();
    public string FactionId => "Enemy";
    public CombatTargetKind Kind => CombatTargetKind.Minion;
    public Vector3 HitPosition => transform.position + Vector3.up * 3f;
    public bool IsTargetable => isActiveAndEnabled;
    public IDamageable DamageReceiver => this;
    public ILifeState LifeState => null;
    public int Hits;
    public DamageResult TakeDamage(DamageInfo damage) { Hits++; return default; }
}

public sealed class MinionCombatRecoveryTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private NavMeshDataInstance navInstance;
    private NavMeshData navData;
    private static readonly Vector3 Origin = new Vector3(14000f, 1000f, 14000f);
    private static Type Runtime(string name) => Type.GetType("Tower." + name + ", Assembly-CSharp", true);
    private static object Call(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name).Invoke(instance, args);
    private static bool Active(object instance) => (bool)instance.GetType().GetProperty("Active").GetValue(instance);
    private static object PrivateCall(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);

    private GameObject Object(string name, Vector3 offset)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        obj.transform.position = Origin + offset;
        return obj;
    }

    [TearDown]
    public void Cleanup()
    {
        if (navInstance.valid) navInstance.Remove();
        if (navData != null) UnityEngine.Object.DestroyImmediate(navData);
        foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        objects.Clear();
    }

    private static bool CanHit(Transform attacker, ICombatTarget target) =>
        (bool)Runtime("MinionAttackSequence").GetMethod("CanHit").Invoke(null, new object[] { attacker, target, 1.5f, 1.5f });

    [Test]
    public void AttackChecksFeetHeightAndRechecksTargetAtImpact()
    {
        var unit = Object("attacker", Vector3.zero);
        var enemy = Object("enemy", Vector3.right).AddComponent<MinionCombatRecoveryTarget>();
        Assert.True(CanHit(unit.transform, enemy), "Tall target aim point must not prevent same-floor melee.");
        enemy.transform.position += Vector3.up * 4f;
        Assert.False(CanHit(unit.transform, enemy), "Cannot hit another platform despite matching XZ.");
        enemy.transform.position = Origin + Vector3.right * 4f;
        Assert.False(CanHit(unit.transform, enemy), "Target left horizontal range during windup.");
        enemy.transform.position = Origin + Vector3.right;
        enemy.enabled = false;
        Assert.False(CanHit(unit.transform, enemy));
        UnityEngine.Object.DestroyImmediate(enemy);
        Assert.False(CanHit(unit.transform, enemy), "Destroyed Unity objects must cancel safely.");
    }

    [TestCase("Skeleton", "Attack", "Standing Melee Attack Downward", 1f)]
    [TestCase("Skeleton", "Attack", "Standing Melee Attack Downward", 2f)]
    [TestCase("Anubis", "Attack", "Mutant Swiping", 1f)]
    [TestCase("Anubis", "UseSkill", "Standing Melee Attack Backhand", 1f)]
    [TestCase("Anubis", "UseSkill", "Standing Melee Attack Backhand", 0.5f)]
    public void RealPrefabAnimatorHitsOnceAtConfiguredProgress(string prefab, string trigger, string stateName, float speed)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/" + prefab + ".prefab");
        var unit = UnityEngine.Object.Instantiate(asset, Origin, Quaternion.identity);
        objects.Add(unit);
        var animator = unit.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = speed;
        var sequence = Activator.CreateInstance(Runtime("MinionAttackSequence"));
        Call(sequence, "Begin", null, stateName, 0.45f, trigger == "UseSkill");
        animator.SetTrigger(trigger);
        int impacts = 0;
        for (int frame = 0; frame < 900 && Active(sequence); frame++)
        {
            animator.Update(1f / 60f);
            if (!(bool)Call(sequence, "Tick", animator, 1f / 60f)) continue;
            impacts++;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName))
                state = animator.GetNextAnimatorStateInfo(0);
            Assert.True(state.IsName(stateName));
            Assert.That(state.normalizedTime, Is.InRange(0.45f, 0.5f));
        }
        Assert.That(impacts, Is.EqualTo(1));
        Assert.False(Active(sequence), "Recovery must finish without queuing another attack.");
    }

    [Test]
    public void InterruptedAttackNeverDealsDelayedDamage()
    {
        var sequence = Activator.CreateInstance(Runtime("MinionAttackSequence"));
        Call(sequence, "Begin", null, "Attack", 0.45f, false);
        Assert.False((bool)Call(sequence, "Tick", null, 0.1f));
        Call(sequence, "Cancel");
        Assert.False((bool)Call(sequence, "Tick", null, 1f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SkeletonDamageOccursAtImpactAndMissesIfTargetChangesFloor(bool leaveFloor)
    {
        var tower = Object("tower", Vector3.zero);
        var status = tower.AddComponent(Runtime("TowerStatus"));
        Runtime("TowerStatus").GetField("coffinConfig", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(status, Activator.CreateInstance(Runtime("CoffinTowerConfig")));
        var unit = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Skeleton.prefab"),
            Origin, Quaternion.identity, tower.transform);
        objects.Add(unit);
        var controller = unit.GetComponent(Runtime("SkeletonController"));
        var movement = unit.GetComponent(Runtime("SkeletonMovement"));
        PrivateCall(controller, "Awake");
        PrivateCall(movement, "Awake");
        Call(controller, "Initialize", null, 0, Origin);
        var enemy = Object("enemy", Vector3.forward).AddComponent<MinionCombatRecoveryTarget>();
        PrivateCall(movement, "FollowAndAttack", enemy);
        Assert.That(enemy.Hits, Is.Zero, "Starting an animation must not deal immediate damage.");
        var animator = unit.GetComponent<Animator>();
        for (int frame = 0; frame < 240; frame++)
        {
            if (leaveFloor && frame == 3) enemy.transform.position += Vector3.up * 5f;
            animator.Update(1f / 60f);
            PrivateCall(movement, "LateUpdate");
        }
        Assert.That(enemy.Hits, Is.EqualTo(leaveFloor ? 0 : 1));
    }

    private bool Step(object recovery, GameObject unit, object settings, float dt, out bool moving)
    {
        object[] args = { unit.transform, Origin, 4f, settings, 4f, dt, false };
        bool result = (bool)Call(recovery, "Step", args);
        moving = (bool)args[6];
        return result;
    }

    [Test]
    public void FallenUnitWithoutPathWaitsThenReturnsToOriginalPlatformHeight()
    {
        var unit = Object("fallen", new Vector3(2f, -5f, 0f));
        var recovery = Activator.CreateInstance(Runtime("MinionReturnToTower"));
        var settings = Activator.CreateInstance(Runtime("MinionReturnSettings"));
        Call(recovery, "Initialize", Origin);
        Assert.True(Step(recovery, unit, settings, 0.1f, out bool moving));
        Assert.False(moving);
        Assert.That(unit.transform.position.y, Is.EqualTo(Origin.y - 5f));
        for (int i = 0; i < 25 && Active(recovery); i++) Step(recovery, unit, settings, 0.1f, out _);
        Assert.That(Vector3.Distance(unit.transform.position, Origin), Is.LessThan(0.01f));
        Assert.False(Active(recovery));
    }

    [Test]
    public void CompleteNavMeshPathWalksBackInsteadOfTeleporting()
    {
        BakeFlatPath();
        var unit = Object("walking", Vector3.right * 6f);
        var recovery = Activator.CreateInstance(Runtime("MinionReturnToTower"));
        var settings = Activator.CreateInstance(Runtime("MinionReturnSettings"));
        Call(recovery, "Initialize", Origin);
        Assert.True(Step(recovery, unit, settings, 0.1f, out bool moving));
        Assert.True(moving);
        Assert.That(unit.transform.position.x - Origin.x, Is.InRange(5.5f, 5.9f));
        for (int i = 0; i < 40 && Active(recovery); i++)
        {
            Vector3 before = unit.transform.position;
            Step(recovery, unit, settings, 0.1f, out _);
            Assert.That(Vector3.Distance(before, unit.transform.position), Is.LessThanOrEqualTo(0.401f));
        }
        Assert.False(Active(recovery));
        Assert.That(Vector3.Distance(unit.transform.position, Origin), Is.LessThan(0.5f));
    }

    private void BakeFlatPath()
    {
        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(Origin + Vector3.down * 0.5f, Quaternion.identity, Vector3.one),
                size = new Vector3(30f, 1f, 30f), area = 0 }
        };
        navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Origin, Vector3.one * 40f), Vector3.zero, Quaternion.identity);
        Assert.NotNull(navData);
        navInstance = NavMesh.AddNavMeshData(navData);
    }

    [Test]
    public void NewlyBlockedPathStopsWalkingThenReturnsHome()
    {
        BakeFlatPath();
        var wall = Object("new obstacle", new Vector3(3f, 1.5f, 0f)).AddComponent<BoxCollider>();
        wall.size = new Vector3(1f, 3f, 6f);
        Physics.SyncTransforms();
        var unit = Object("blocked", Vector3.right * 6f);
        var recovery = Activator.CreateInstance(Runtime("MinionReturnToTower"));
        var settings = Activator.CreateInstance(Runtime("MinionReturnSettings"));
        Call(recovery, "Initialize", Origin);
        bool walked = false, waited = false;
        for (int i = 0; i < 80; i++)
        {
            Step(recovery, unit, settings, 0.1f, out bool moving);
            walked |= moving;
            waited |= !moving && Active(recovery);
            if (!Active(recovery)) break;
        }
        Assert.True(walked);
        Assert.True(waited);
        Assert.False(Active(recovery));
        Assert.That(Vector3.Distance(unit.transform.position, Origin), Is.LessThan(0.01f));
    }
}
