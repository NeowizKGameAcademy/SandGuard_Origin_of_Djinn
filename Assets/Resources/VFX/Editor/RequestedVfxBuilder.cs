using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    public static class RequestedVfxBuilder
    {
        public const string ShieldPath = PrefabDir + "/VFX_Shield_Front_Guard.prefab";
        public const string GoldShieldPath = PrefabDir + "/VFX_Shield_Gold_Guard.prefab";
        public const string FacilityPath = PrefabDir + "/VFX_Facility_Hit.prefab";
        public const string BombPath = PrefabDir + "/VFX_Demolition_Bomb_Explosion.prefab";
        public const string DisabledPath = PrefabDir + "/VFX_Facility_Disabled_Loop.prefab";
        public const string CobraPath = "Assets/2.Model/Prefabs/Tower (Cobra).prefab";

        public static void BuildLightningAndPreview()
        {
            BuildDisabled();
            VfxShowcaseBuilder.Build();
            RequestedVfxPreview.Render();
        }

        public static Bounds CobraBounds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CobraPath);
            if (prefab == null) throw new System.InvalidOperationException("Cobra reference missing: " + CobraPath);
            bool found = false;
            var bounds = new Bounds();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled || !filter.gameObject.activeSelf) continue;
                var local = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var point = filter.transform.TransformPoint(new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found) throw new System.InvalidOperationException("Cobra has no visible mesh.");
            return bounds;
        }
        public static readonly string[] Paths = { ShieldPath, FacilityPath, CombatVfxBuilder.PlayerHitPath,
            BombPath, DisabledPath, ImpactVfxBuilder.CoreDamageFlashPath, ExperienceMoteBuilder.PrefabPath, GoldVfxBuilder.LevelUpPath };

        [MenuItem("DesertTower/VFX/Build Requested Set (8, No UI)")]
        public static void Build()
        {
            BuildShield();
            ImpactVfxBuilder.Build(new ImpactSpec {
                PrefabName = "VFX_Facility_Hit", FlashSize = 0.85f, FlashColor = new Color(1f, 0.87f, 0.65f),
                CoreCount = 2, CoreSize = 0.14f, CoreA = Color.white, CoreB = Orange, CoreC = GoldDark,
                Shockwave = false, DebrisCount = 9, DebrisSize = new Vector2(0.04f, 0.1f),
                DebrisSpeed = new Vector2(1f, 2.4f), DebrisLife = new Vector2(0.3f, 0.6f),
                DebrisA = Beige, DebrisB = BeigeDark, DebrisC = BeigeDark, EmberCount = 4,
                EmberLife = new Vector2(0.15f, 0.35f), EmberColor = Orange, LightIntensity = 1.5f, LightRange = 1.5f
            }, FacilityPath);
            ImpactVfxBuilder.Build(CombatVfxBuilder.PlayerHit(), CombatVfxBuilder.PlayerHitPath);
            ImpactVfxBuilder.Build(new ImpactSpec {
                PrefabName = "VFX_Demolition_Bomb_Explosion", BodyHeight = 0.25f, GroundCollision = true,
                FlashSize = 2.4f, FlashColor = new Color(1f, 0.88f, 0.65f),
                CoreCount = 6, CoreSize = 0.5f, CoreA = Color.white, CoreB = Orange, CoreC = Red,
                ShockwaveSize = 4.5f, ShockwaveColor = Red, ShockwaveCount = 2, ShockwaveInterval = 0.09f,
                DebrisCount = 32, DebrisSize = new Vector2(0.09f, 0.24f), DebrisSpeed = new Vector2(2f, 5f),
                DebrisLife = new Vector2(0.65f, 1.1f), DebrisA = Orange, DebrisB = BeigeDark, DebrisC = Smoke,
                DebrisShape = DebrisShape.ConeUp, DebrisConeAngle = 75f,
                EmberCount = 18, EmberCubes = true, EmberColor = Red,
                EmberLife = new Vector2(0.6f, 1.2f), EmberSpeed = new Vector2(0.5f, 1.3f),
                DustCount = 14, DustColor = new Color(0.12f, 0.09f, 0.08f, 0.85f),
                DustSize = new Vector2(0.7f, 1.3f), DustLife = new Vector2(0.9f, 1.6f),
                LightIntensity = 5f, LightRange = 5f
            }, BombPath);
            BuildDisabled();
            ImpactVfxBuilder.Build(ImpactVfxBuilder.CoreDamageFlash(), ImpactVfxBuilder.CoreDamageFlashPath);
            ExperienceMoteBuilder.Build();
            GoldVfxBuilder.BuildLevelUp();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("DesertTower/VFX/Build Shield Guards (Standard + Chief Gold)")]
        public static void BuildShield()
        {
            BuildShieldVariant(ShieldPath, false);
            BuildShieldVariant(GoldShieldPath, true);
        }

        static void BuildShieldVariant(string path, bool golden)
        {
            var spec = new ImpactSpec { PrefabName = System.IO.Path.GetFileNameWithoutExtension(path), FlashSize = 0.55f,
                CoreCount = 1, CoreSize = 0.08f, CoreA = Color.white, CoreB = Gold, CoreC = GoldDark,
                Shockwave = false, DebrisCount = 4, DebrisSize = new Vector2(0.035f, 0.07f),
                DebrisSpeed = new Vector2(1.5f, 2.5f), DebrisLife = new Vector2(0.2f, 0.4f),
                DebrisA = Color.white, DebrisB = Gold, DebrisC = GoldDark,
                EmberCount = 0, LightIntensity = 1.2f, LightRange = 1f };
            if (golden)
            {
                spec.FlashColor = new Color(1f, .86f, .38f);
                spec.CoreA = new Color(1f, .95f, .66f); spec.CoreB = new Color(1f, .65f, .12f); spec.CoreC = new Color(.7f, .30f, .035f);
                spec.DebrisA = spec.CoreA; spec.DebrisB = spec.CoreB; spec.DebrisC = spec.CoreC;
            }
            ImpactVfxBuilder.Build(spec, path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var points = new Vector3[7];
                for (int i = 0; i < points.Length; i++)
                {
                    float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                    points[i] = new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.4f, 0.025f);
                }
                Stroke(root, "ShieldSurfaceFlash", points, golden ? new Color(1f, .72f, .18f) : new Color(0.8f, 0.9f, 1f), false, 0f, 0.018f);
                var ps = root.transform.Find("Debris").GetComponent<ParticleSystem>();
                ConeShape(ps, 60f, 0.04f); // local +Z points OUT of the shield, never a full bubble.
                root.transform.Find("Debris").localRotation = Quaternion.identity;
                var shield = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Art/Equipment/TowerShield.prefab");
                if (!shield) throw new System.InvalidOperationException("TowerShield reference missing");
                var bounds = new Bounds(); bool first = true;
                foreach (var f in shield.GetComponentsInChildren<MeshFilter>(true))
                    for (int i = 0; i < 8; i++)
                    {
                        var b=f.sharedMesh.bounds;
                        var p=f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                    }
                var axes = new[]{bounds.size.x,bounds.size.y,bounds.size.z}; System.Array.Sort(axes);
                if(first || axes[1]<=0) throw new System.InvalidOperationException("Invalid shield bounds");
                // Twelve percent wider/taller than the actual tower shield; both variants share this scale.
                float sx=axes[1]*1.12f/(Mathf.Sqrt(3f)*.3f), sy=axes[2]*1.12f/.8f;
                root.transform.localScale=new Vector3(sx,sy,Mathf.Min(sx,sy));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("DesertTower/VFX/Build Facility Disabled (Cobra Size)")]
        public static void BuildDisabled()
        {
            var s = GetShared();
            var bounds = CobraBounds();
            Debug.Log($"[VFX] Cobra reference bounds: center={bounds.center:F3}, size={bounds.size:F3}");
            var root = new GameObject("VFX_Facility_Disabled_Loop");
            AddHub(root);
            root.AddComponent<VfxDisabledVisual>();
            var guide = root.AddComponent<VfxLightningGuide>();
            guide.Center = bounds.center;
            guide.Radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.3f;
            var random = new System.Random(914);
            for (int a = 0; a < 5; a++)
            {
                var rotation = Quaternion.Euler(25f + a * 27f, a * 72f, 35f - a * 19f);
                var from = rotation * new Vector3(-0.85f, 0.5f, 0.15f).normalized;
                var to = rotation * new Vector3(0.75f, 0.3f, -0.55f).normalized;
                var points = SpherePath(from, to, guide, 31, random);
                var mainPath = Lightning(root, "Discharge " + (a + 1), points, a * 0.17f, 0f, 0.32f, 0.032f, s);
                for (int fork = 0; fork < 2; fork++)
                {
                    int junction = fork == 0 ? 10 : 20;
                    var start = (points[junction] - guide.Center).normalized;
                    var tangent = Vector3.Cross(start, rotation * Vector3.up).normalized;
                    var end = (start + tangent * (fork == 0 ? 0.4f : -0.35f)).normalized;
                    var branch = SpherePath(start, end, guide, 9, random);
                    float distance = 0f, total = 0f;
                    for (int i = 1; i < points.Length; i++)
                    {
                        float segment = Vector3.Distance(points[i - 1], points[i]);
                        total += segment; if (i <= junction) distance += segment;
                    }
                    Lightning(root, "Fork " + (a + 1) + "." + (fork + 1), branch, mainPath.Phase,
                        mainPath.TravelTime * distance / total, 0.11f, 0.018f, s);
                }
            }
            SavePrefab(root, DisabledPath);
        }

        static Vector3[] SpherePath(Vector3 from, Vector3 to, VfxLightningGuide guide, int count, System.Random random)
        {
            var result = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var direction = Vector3.Slerp(from, to, i / (float)(count - 1));
                if (i > 0 && i < count - 1)
                    direction += new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f,
                        (float)random.NextDouble() - 0.5f) * 0.07f;
                result[i] = guide.Center + direction.normalized * guide.Radius;
            }
            return result;
        }

        static VfxLightningPath Lightning(GameObject parent, string name, Vector3[] points, float phase, float delay, float travel, float width, Shared shared)
        {
            var go = Child(parent, name);
            var path = go.AddComponent<VfxLightningPath>();
            path.Points = points; path.Phase = phase; path.Delay = delay; path.TravelTime = travel;
            path.Glow = LightningLine(Child(go, "Red Afterglow"), shared.MeshAdditive, width);
            path.Core = LightningLine(Child(go, "White Hot Core"), shared.MeshAdditive, width * 0.32f);
            var ps = AddSystem(Child(go, "Head Sparks"), out var renderer, loop: true);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.startColor = new Color(1f, 0.55f, 0.2f); main.maxParticles = 5;
            Rate(ps, 0f); SphereShape(ps, 0.01f); Size(ps, Linear(1f, 0f));
            UseMesh(renderer, shared.Cube, shared.CubeWhite);
            path.Sparks = ps; path.Restart();
            return path;
        }

        static LineRenderer LightningLine(GameObject go, Material material, float width)
        {
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = 0; line.widthMultiplier = width;
            line.sharedMaterial = material; line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        static void Stroke(GameObject parent, string name, Vector3[] points, Color tint, bool loop, float phase, float width)
        {
            var go = Child(parent, name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = width; line.numCornerVertices = 0; line.numCapVertices = 0;
            line.sharedMaterial = GetShared().MeshAdditive;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var pulse = go.AddComponent<VfxArcPulse>();
            pulse.Tint = tint; pulse.Loop = loop; pulse.Phase = phase; pulse.Duration = 0.25f;
            pulse.Restart();
        }
    }
}
