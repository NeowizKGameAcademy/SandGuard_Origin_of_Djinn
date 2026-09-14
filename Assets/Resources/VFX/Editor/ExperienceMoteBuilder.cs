using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    /// <summary>Persistent XP pickup visual. Spawning, collection and XP rewards belong to gameplay.</summary>
    public static class ExperienceMoteBuilder
    {
        public const string PrefabPath = PrefabDir + "/VFX_Experience_Mote.prefab";
        public const string MaterialPath = MatDir + "/M_VFX_Experience_Gold.mat";

        [MenuItem("DesertTower/VFX/Build Experience Mote")]
        public static void BuildFromMenu() => EditorGUIUtility.PingObject(Build());

        public static GameObject Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ManaBoltProjectileBuilder.PrefabPath);
            if (source == null) source = ManaBoltProjectileBuilder.Build();
            var root = Object.Instantiate(source);
            root.name = "VFX_Experience_Mote";
            var core = root.transform.Find("Core");
            var glow = root.transform.Find("Glow");
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child != core && child != glow) Object.DestroyImmediate(child.gameObject);
            }

            var gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var sourceMaterial = core.GetComponent<MeshRenderer>().sharedMaterial;
            if (gold == null)
            {
                gold = new Material(sourceMaterial);
                AssetDatabase.CreateAsset(gold, MaterialPath);
            }
            else gold.CopyPropertiesFromMaterial(sourceMaterial);
            gold.name = "M_VFX_Experience_Gold";
            var tint = Gold * 2.5f;
            tint.a = 1f;
            gold.SetColor("_BaseColor", tint);
            gold.SetColor("_Color", tint);
            EditorUtility.SetDirty(gold);

            core.localPosition = new Vector3(0f, 0.35f, 0f);
            core.localScale = Vector3.one * 0.16f;
            core.GetComponent<MeshRenderer>().sharedMaterial = gold;
            core.GetComponent<VfxSpin>().DegreesPerSecond = new Vector3(0f, 90f, 30f);
            glow.localPosition = core.localPosition;
            glow.localScale = Vector3.one * 0.45f;
            var main = glow.GetComponent<ParticleSystem>().main;
            main.startColor = Gold;
            return SavePrefab(root, PrefabPath);
        }
    }
}
