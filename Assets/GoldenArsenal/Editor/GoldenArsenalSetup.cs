using UnityEditor;
using UnityEngine;
using System.IO;

public static class GoldenArsenalSetup
{
    const string Root = "Assets/GoldenArsenal";
    [MenuItem("Tools/Golden Arsenal/Build Materials and Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        string[] names = { "GA_Gold", "GA_GoldLight", "GA_GoldShadow", "GA_Silver", "GA_Obsidian", "GA_Grip", "GA_Turquoise", "GA_GemLight" };
        Color[] colors = { new Color(.98f,.65f,.13f), new Color(1,.85f,.33f), new Color(.65f,.38f,.08f), new Color(.86f,.87f,.84f), new Color(.23f,.23f,.25f), new Color(.29f,.23f,.18f), new Color(.04f,.8f,.87f), new Color(.17f,.96f,.98f) };
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new System.Exception("URP Lit shader missing");
        var mats = new Material[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = Root + "/Materials/" + names[i] + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", colors[i]);
            m.SetFloat("_Metallic", i == 5 ? 0 : i == 4 ? .25f : .65f);
            m.SetFloat("_Smoothness", i == 5 ? .2f : .65f);
            if (i >= 6) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", colors[i] * .35f); }
            EditorUtility.SetDirty(m); mats[i] = m;
        }
        foreach (string name in new[] { "GoldenSword", "GoldenShield", "GoldenSpear" })
        {
            string path = Root + "/Models/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = new GameObject(name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int j = 0; j < slots.Length; j++)
                {
                    string slot = slots[j] ? slots[j].name : "";
                    int index = System.Array.IndexOf(names, slot);
                    if (index < 0) throw new System.Exception("Unknown material: " + slot);
                    slots[j] = mats[index];
                }
                renderer.sharedMaterials = slots;
            }
            var marker = new GameObject("GripPoint"); marker.transform.SetParent(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + name + ".prefab");
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log("GOLDEN_ARSENAL_VERIFIED " + name + " size=" + bounds.size.ToString("F3"));
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("GOLDEN_ARSENAL_BUILD_OK");
    }
}
