using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 상승 기류(스킬 ③) 발사 이펙트. 플레이어 발밑에 스폰: 넓은 모래색 충격파 링 두 겹 + 모래 알갱이 파편 + 큰 모래먼지 구름 + 위로 솟는 모래 기둥.
    /// 임팩트 레이어는 <see cref="ImpactVfxBuilder"/>의 스펙으로, 기둥은 후처리로 붙인다. 런타임 연결은 CombatVfxWiring이
    /// PlayerUpdraft.onLaunched → VfxOneShot.Fire로 꽂는다.
    /// </summary>
    public static class UpdraftVfxBuilder
    {
        public const string LaunchPath = PrefabDir + "/VFX_Updraft_Launch.prefab";
        public const string ChargePath = PrefabDir + "/VFX_Updraft_Charge.prefab";
        public const string LensPath = PrefabDir + "/VFX_Updraft_Lens.prefab";
        public const string LensMaterialPath = MatDir + "/M_VFX_ScreenRefraction.mat";
        public const string LensShaderName = "SandGuard/VFX/ScreenRefraction";

        [MenuItem("DesertTower/VFX/Build Updraft (charge + launch)")]
        public static void BuildFromMenu() { Build(); EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(LaunchPath)); }

        public static GameObject Build()
        {
            ImpactVfxBuilder.Build(Launch(), LaunchPath);
            AddColumn();
            BuildCharge();
            BuildLens();
            return AssetDatabase.LoadAssetAtPath<GameObject>(LaunchPath);
        }

        /// <summary>
        /// 볼록 렌즈: 화면 굴절 셰이더를 씌운 구체 하나. <see cref="VfxRefractionBubble"/>이 충전량으로 반지름·세기를 키우고 발사 때 펄스를 낸다.
        /// URP 파이프라인 에셋의 Opaque Texture가 켜져 있어야 보인다(PC 에셋은 켜져 있음).
        /// </summary>
        public static GameObject BuildLens()
        {
            var shader = Shader.Find(LensShaderName);
            if (shader == null) throw new System.InvalidOperationException("[VFX] Shader missing: " + LensShaderName + " (Assets/Resources/VFX/Shaders/VFX_ScreenRefraction.shader)");
            var material = AssetDatabase.LoadAssetAtPath<Material>(LensMaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, LensMaterialPath); }
            material.shader = shader;
            material.SetFloat("_Strength", 0f);
            material.SetFloat("_EdgePower", 2.2f);
            material.SetColor("_Tint", new Color(1f, 0.96f, 0.88f, 0.15f));      // 살짝 모래빛
            material.SetColor("_RimColor", new Color(0.85f, 0.72f, 0.48f, 0.12f)); // 가장자리 희미한 테두리
            EditorUtility.SetDirty(material);

            var root = new GameObject("VFX_Updraft_Lens");
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var bubble = root.AddComponent<VfxRefractionBubble>();
            bubble.Target = renderer;
            return SavePrefab(root, LensPath);
        }

        /// <summary>
        /// 충전 기류: 몸 주변을 도는 모래 줄기(스트레치 빌보드) + 바깥에서 몸으로 빨려드는 모래 알갱이 + 발밑에서 피어오르는 잔먼지.
        /// 세 시스템 모두 루프이며 <see cref="VfxChargeLoop"/>가 충전량으로 방출량·속도·크기를 키운다. 원점은 몸 중심(허리 높이)에 자식으로 붙인다.
        /// </summary>
        public static GameObject BuildCharge()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Updraft_Charge");
            AddHub(root);

            // 1. 몸을 도는 모래 줄기: 허리 높이 반지름 0.75m 원에서 나와 궤도 회전 + 안쪽으로 조여든다.
            var swirl = AddSystem(Child(root, "Swirl"), out var swirlRenderer, loop: true);
            {
                var main = swirl.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(Beige, Beige * 0.7f);
                main.maxParticles = 160;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Rate(swirl, 45f);
                CircleShape(swirl, 0.75f, 0.25f);
                var swirlShape = swirl.shape; swirlShape.rotation = new Vector3(90f, 0f, 0f); // 원을 눕혀 몸 둘레의 수평 고리로
                var velocity = swirl.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; // 회전축 = 시스템의 Y(위)
                // 모듈의 모든 곡선은 같은 모드여야 한다(TwoConstants). 안 쓰는 축은 0~0.
                velocity.x = Range(0f, 0f); velocity.y = Range(0.4f, 1.2f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(5f, 8f); velocity.orbitalZ = Range(0f, 0f);
                velocity.radial = Range(-0.9f, -0.5f);
                Size(swirl, InOut(0.15f, 0.6f));
                AlphaFade(swirl, Color.white, 0.4f);
                UseBillboard(swirlRenderer, s.GlowSoft, ParticleSystemRenderMode.Stretch);
                swirlRenderer.velocityScale = 0.08f; swirlRenderer.lengthScale = 2.5f;
                swirlRenderer.sortingFudge = 2f;
            }

            // 2. 빨려드는 모래 알갱이: 바깥 1.4m 원에서 몸 중심으로 모인다 (응축).
            var pull = AddSystem(Child(root, "Pull"), out var pullRenderer, loop: true);
            {
                var main = pull.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                main.maxParticles = 120;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                RandomRotation3D(main);
                Rate(pull, 30f);
                CircleShape(pull, 1.4f, 0.15f);
                var pullShape = pull.shape; pullShape.rotation = new Vector3(90f, 0f, 0f);
                var velocity = pull.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = Range(0f, 0f); velocity.y = Range(0f, 0f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(1.5f, 3f); velocity.orbitalZ = Range(0f, 0f);
                velocity.radial = Range(-2.4f, -1.6f);
                Tumble(pull, Mathf.PI * 2f);
                ColorRampFade(pull, Beige * 0.5f, Beige * 0.4f, BeigeDark * 0.7f, 0.4f, 0.7f); // HDR 큐브 재질(×2.5)이라 어둡게 넣어야 모래색이 남는다
                Size(pull, EaseOut(1f, 0.3f));
                UseMesh(pullRenderer, s.Cube, s.CubeWhite);
                pullRenderer.sortingFudge = 1f;
            }

            // 3. 발밑 잔먼지: 바닥에서 천천히 피어오르는 작은 연기 조각.
            var feet = AddSystem(Child(root, "Feet"), out var feetRenderer, loop: true);
            {
                var main = feet.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                main.startColor = new Color(0.62f, 0.50f, 0.32f, 0.55f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.maxParticles = 40;
                Rate(feet, 14f);
                CircleShape(feet, 0.6f, 0.6f);
                feet.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                feet.transform.localPosition = new Vector3(0f, -0.9f, 0f); // 루트는 허리 높이(0.95m)에 붙으므로 발밑
                Noise(feet, 0.15f, 0.3f, 0.3f);
                Size(feet, EaseOut(0.5f, 1.2f));
                AlphaFade(feet, Color.white, 0.2f);
                RandomTile(feet, 2, 2);
                UseBillboard(feetRenderer, s.Smoke);
                feetRenderer.sortingFudge = 5f;
            }

            var loop = root.AddComponent<VfxChargeLoop>();
            loop.Systems = new[] { swirl, pull, feet };
            return SavePrefab(root, ChargePath);
        }

        /// <summary>착지 먼지의 큰 형: 링 3.4m 두 겹, 파편 36, 먼지 20. 빛은 없다(모래).</summary>
        public static ImpactSpec Launch() => new ImpactSpec
        {
            PrefabName = "VFX_Updraft_Launch",
            Scale = 1f,
            GroundCollision = true,
            BodyHeight = 0.15f,
            FlashSize = 1.2f, FlashColor = Beige * 0.9f,
            CoreCount = 0,
            Shockwave = true, ShockwaveSize = 3.4f, ShockwaveColor = Beige, ShockwaveCount = 2, ShockwaveInterval = 0.09f,
            DebrisCount = 36,
            DebrisSize = new Vector2(0.05f, 0.11f),
            DebrisSpeed = new Vector2(3f, 6f),
            DebrisLife = new Vector2(0.5f, 0.9f),
            DebrisGravity = 1.1f,
            DebrisA = Beige * 0.7f, DebrisB = BeigeDark, DebrisC = BeigeDark * 0.6f,
            DebrisShape = DebrisShape.ConeUp, DebrisConeAngle = 70f,
            EmberCount = 0,
            DustCount = 20,
            DustSize = new Vector2(0.7f, 1.2f),
            DustSpeed = new Vector2(1.4f, 2.6f),
            DustLife = new Vector2(0.9f, 1.5f),
            DustStartSize = 0.6f,
            LightIntensity = 0f,
        };

        static ParticleSystem.MinMaxCurve Range(float min, float max) => new ParticleSystem.MinMaxCurve(min, max);

        /// <summary>발사 순간 몸을 따라 위로 솟는 모래 기둥. 좁은 원뿔로 빠르게 올라가며 흩어진다.</summary>
        static void AddColumn()
        {
            var s = GetShared();
            var root = PrefabUtility.LoadPrefabContents(LaunchPath);
            try
            {
                var existing = root.transform.Find("Column");
                var column = existing != null ? existing.gameObject : Child(root, "Column");
                var ps = column.GetComponent<ParticleSystem>();
                ParticleSystemRenderer r;
                if (ps == null) ps = AddSystem(column, out r); else r = column.GetComponent<ParticleSystemRenderer>();
                var main = ps.main;
                main.duration = 0.6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startColor = new Color(0.66f, 0.54f, 0.35f, 0.7f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.maxParticles = 24;
                Burst(ps, 16);
                ConeShape(ps, 12f, 0.35f);
                column.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 원뿔이 위를 본다
                column.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                Drag(ps, 0.6f, 2f);
                Noise(ps, 0.25f, 0.4f, 0.4f);
                Size(ps, EaseOut(0.5f, 1.4f));
                AlphaFade(ps, Color.white, 0.2f);
                RandomTile(ps, 2, 2);
                UseBillboard(r, s.Smoke);
                r.sortingFudge = 4f;
                PrefabUtility.SaveAsPrefabAsset(root, LaunchPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
