using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class MinionGroundingTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private static readonly Vector3 Origin = new Vector3(12000f, 1000f, 12000f);

    private GameObject Box(Vector3 offset, Vector3 scale)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(obj);
        obj.transform.position = Origin + offset;
        obj.transform.localScale = scale;
        return obj;
    }

    private static Vector3 Project(GameObject unit)
    {
        Physics.SyncTransforms();
        var type = Type.GetType("Tower.MinionGrounding, Assembly-CSharp", true);
        return (Vector3)type.GetMethod("Project").Invoke(null,
            new object[] { unit.transform, unit.transform.position });
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var obj in objects) UnityEngine.Object.DestroyImmediate(obj);
        objects.Clear();
    }

    [Test]
    public void ElevatedSpawnLandsOnGroundIgnoringSelfAndTriggers()
    {
        Box(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
        Box(Vector3.up, new Vector3(10f, 0.2f, 10f)).GetComponent<Collider>().isTrigger = true;
        var unit = Box(Vector3.up * 4f, Vector3.one);
        Assert.That(Project(unit).y, Is.EqualTo(Origin.y).Within(0.001f));
    }

    [Test]
    public void GroundHeightFollowsSlopeInBothDirections()
    {
        var ramp = Box(Vector3.zero, new Vector3(20f, 0.5f, 20f));
        ramp.transform.rotation = Quaternion.Euler(0f, 0f, 15f);
        var unit = Box(new Vector3(-3f, 4f, 0f), Vector3.one);
        Vector3 low = Project(unit);
        unit.transform.position = new Vector3(Origin.x + 3f, low.y, Origin.z);
        Vector3 high = Project(unit);
        Assert.That(high.y - low.y, Is.EqualTo(6f * Mathf.Tan(15f * Mathf.Deg2Rad)).Within(0.01f));
        unit.transform.position = new Vector3(low.x, high.y, low.z);
        Assert.That(Project(unit).y, Is.EqualTo(low.y).Within(0.001f));
    }

    [Test]
    public void MissingGroundPreservesPosition()
    {
        var unit = Box(Vector3.up * 4f, Vector3.one);
        Assert.That(Project(unit), Is.EqualTo(unit.transform.position));
    }
}
