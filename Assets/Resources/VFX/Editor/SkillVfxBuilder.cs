using DesertTower.VFX;
using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 플레이어 스킬 연출 중 임팩트·지역 빌더에 안 들어가는 것들.
    ///   VFX_Pierce_Charge — 관통탄 충전: 손 주위를 도는 파란 마나 줄기 + 빨려드는 알갱이 + 커지는 코어. VfxChargeLoop(Begin/SetIntensity/End)로 세기 조절. 손(CastingEffectAnchor)에 자식으로.
    ///   VFX_Recall_Mark  — 흔적 귀환 표식: 금빛 룬 두 겹 + 솟는 알갱이 + 맥동 광원. VfxRecallMark가 남은 시간으로 어둡게, 풀릴 때 번지며 지움. 바닥에 놓는다.
    ///   VFX_Sand_Burst   — 기존 임팩트에 지면 균열 룬(Crack)을 덧붙인다(빌드 순서상 임팩트 다음).
    /// 색: 관통탄 계열은 파랑(마나탄의 틸과 구분), 모래 계열은 금빛.
    /// </summary>
    public static class SkillVfxBuilder
    {
        public const string PierceChargePath = PrefabDir + "/VFX_Pierce_Charge.prefab";
        public const string RecallMarkPath = PrefabDir + "/VFX_Recall_Mark.prefab";
        public const string MatMagicCircleGoldPath = MatDir + "/M_VFX_MagicCircle_Gold.mat";
        public static readonly Color Blue = new Color(0.31f, 0.66f, 1f);
        public static readonly Color BlueDark = new Color(0.06f, 0.16f, 0.4f);

        [MenuItem("DesertTower/VFX/Build Skill Set (pierce charge + recall mark + burst crack)")]
        public static void BuildFromMenu() { Build(); EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PierceChargePath)); }

        public static void Build() { BuildPierceCharge(); BuildRecallMark(); AddBurstCrack(); }

        /// <summary>
        /// 배치용: 큐 → 스킬 관련 VFX(폭발·관통 히트·귀환 임팩트, 관통 빔, 지역 2종, 이 빌더) → 플레이어 프리팹 배선 → 오디오 배선.
        /// 전체 빌드(VfxBatch.BuildAll) 대신 스킬 프리팹만 다시 만든다(다른 빌더의 누락 참조에 막히지 않도록).
        /// </summary>
        [MenuItem("DesertTower/VFX/Build + Wire Skill Set (cues, skill VFX, player prefab, audio)")]
        public static void BuildAndWireAll()
        {
            SandGuard.Audio.Editor.AudioWiring.All();
            BuildShared();
            ImpactVfxBuilder.Build(ImpactVfxBuilder.SandBurst(), ImpactVfxBuilder.SandBurstPath);
            ImpactVfxBuilder.Build(ImpactVfxBuilder.PierceHit(), ImpactVfxBuilder.PierceHitPath);
            ImpactVfxBuilder.Build(ImpactVfxBuilder.RecallDepart(), ImpactVfxBuilder.RecallDepartPath);
            ImpactVfxBuilder.Build(ImpactVfxBuilder.RecallArrive(), ImpactVfxBuilder.RecallArrivePath);
            BeamVfxBuilder.BuildPierceBeam();
            SandZoneVfxBuilder.Build();
            Build();
            AssetDatabase.SaveAssets();
            CombatVfxWiring.WirePlayerPrefab();
            SandGuard.Audio.Editor.AudioWiring.WireAll(); // 다시 만든 프리팹의 소리 이미터 복구 포함
            AssetDatabase.SaveAssets();
            Debug.Log("[VFX] Skill set built and wired.");
        }

        public static Material MagicCircleGold()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MagicCircleTexPath);
            return BuildMaterial(MatMagicCircleGoldPath, tex, Gold * 2.5f, opaque: false);
        }

        static ParticleSystem.MinMaxCurve Range(float min, float max) => new ParticleSystem.MinMaxCurve(min, max);

        // ------------------------------------------------------------------
        public static GameObject BuildPierceCharge()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Pierce_Charge");
            AddHub(root);

            // 1. 손을 도는 마나 줄기: 반지름 0.3 고리에서 궤도 회전하며 손으로 조여든다.
            var orbit = AddSystem(Child(root, "Orbit"), out var orbitRenderer, loop: true);
            {
                var main = orbit.main;
                main.startLifetime = Range(0.35f, 0.6f);
                main.startSpeed = Range(0.1f, 0.3f);
                main.startSize = Range(0.03f, 0.05f);
                main.startColor = new ParticleSystem.MinMaxGradient(Blue, Color.Lerp(Blue, Color.white, 0.5f));
                main.maxParticles = 120;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Rate(orbit, 40f);
                SphereShape(orbit, 0.3f);
                var velocity = orbit.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = Range(0f, 0f); velocity.y = Range(0f, 0f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(6f, 10f); velocity.orbitalZ = Range(3f, 6f);
                velocity.radial = Range(-0.5f, -0.2f);
                Size(orbit, InOut(0.15f, 0.6f));
                AlphaFade(orbit, Color.white, 0.4f);
                UseBillboard(orbitRenderer, s.GlowSoft, ParticleSystemRenderMode.Stretch);
                orbitRenderer.velocityScale = 0.06f; orbitRenderer.lengthScale = 2.2f;
                orbitRenderer.sortingFudge = 2f;
            }

            // 2. 빨려드는 알갱이: 0.8m 구에서 손으로 모인다.
            var pull = AddSystem(Child(root, "Pull"), out var pullRenderer, loop: true);
            {
                var main = pull.main;
                main.startLifetime = Range(0.3f, 0.45f);
                main.startSpeed = 0f;
                main.startSize = Range(0.02f, 0.045f);
                main.maxParticles = 90;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                RandomRotation3D(main);
                Rate(pull, 34f);
                SphereShape(pull, 0.8f);
                var velocity = pull.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = Range(0f, 0f); velocity.y = Range(0f, 0f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(1f, 2.5f); velocity.orbitalZ = Range(0f, 0f);
                velocity.radial = Range(-3f, -2f);
                Tumble(pull, Mathf.PI * 2f);
                ColorRampFade(pull, Color.white, Blue, BlueDark, 0.3f, 0.7f);
                Size(pull, EaseOut(1f, 0.3f));
                UseMesh(pullRenderer, s.Cube, s.CubeWhite);
                pullRenderer.sortingFudge = 1f;
            }

            // 3. 코어: 손 안에서 맥동하는 빛. 세기에 따라 커진다(VfxChargeLoop MaxSize). 대낮 신전에서 블룸이 터지지 않게 작고 부드럽게.
            var core = AddSystem(Child(root, "Core"), out var coreRenderer, loop: true);
            {
                var main = core.main;
                main.startLifetime = 0.3f;
                main.startSpeed = 0f;
                main.startSize = Range(0.06f, 0.09f);
                main.startColor = new Color(Blue.r, Blue.g, Blue.b, 0.85f);
                main.maxParticles = 4;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                Rate(core, 6f);
                Size(core, InOut(0.2f, 0.6f));
                UseBillboard(coreRenderer, s.GlowSoft);
                coreRenderer.sortingFudge = -5f;
                ParticleLight(core, s.LightPrefab, 1.2f, 1.5f, ratio: 0.25f);
            }

            var loop = root.AddComponent<VfxChargeLoop>();
            loop.Systems = new[] { orbit, pull, core };
            loop.MinRate = 0.3f; loop.MaxRate = 1.8f;
            loop.MinSpeed = 0.8f; loop.MaxSpeed = 1.6f;
            loop.MinSize = 0.6f; loop.MaxSize = 1.7f;
            return SavePrefab(root, PierceChargePath);
        }

        // ------------------------------------------------------------------
        public static GameObject BuildRecallMark()
        {
            var s = GetShared();
            var gold = MagicCircleGold();
            var root = new GameObject("VFX_Recall_Mark");
            AddHub(root);

            var outer = Child(root, "Circle Outer");
            outer.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var outerRenderer = GroundQuad(outer, s.Quad, gold, 1.3f, 25f);

            var inner = Child(root, "Circle Inner");
            inner.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var innerRenderer = GroundQuad(inner, s.Quad, gold, 0.8f, -40f);

            // 솟는 금빛 알갱이.
            var rise = AddSystem(Child(root, "Rise"), out var riseRenderer, loop: true);
            {
                var main = rise.main;
                main.startLifetime = Range(0.9f, 1.3f);
                main.startSpeed = 0f;
                main.startSize = Range(0.05f, 0.09f);
                main.startColor = Gold;
                main.maxParticles = 40;
                RandomRotation3D(main);
                Rate(rise, 10f);
                CircleShape(rise, 0.5f, 1f);
                var shape = rise.shape; shape.rotation = new Vector3(90f, 0f, 0f);
                var velocity = rise.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = Range(0f, 0f); velocity.y = Range(0.6f, 1.1f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(0f, 0f); velocity.orbitalZ = Range(0f, 0f);
                velocity.radial = Range(0f, 0f);
                Tumble(rise, Mathf.PI);
                ColorRamp(rise, Color.white, Gold, GoldDark, 0.3f);
                Size(rise, InOut(0.1f, 0.7f));
                UseMesh(riseRenderer, s.Cube, s.CubeWhite);
            }

            // 맥동하는 광원.
            var glow = AddSystem(Child(root, "Glow"), out var glowRenderer, loop: true);
            {
                glow.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                var main = glow.main;
                main.startLifetime = 0.7f;
                main.startSpeed = 0f;
                main.startSize = Range(0.9f, 1.2f);
                main.startColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
                main.maxParticles = 3;
                Rate(glow, 2f);
                Size(glow, InOut(0.3f, 0.6f));
                UseBillboard(glowRenderer, s.GlowSoft);
                glowRenderer.sortingFudge = 3f;
                ParticleLight(glow, s.LightPrefab, 2f, 3f, ratio: 0.5f);
            }

            var mark = root.AddComponent<VfxRecallMark>();
            mark.fadeRenderers = new Renderer[] { outerRenderer, innerRenderer };
            mark.loops = new[] { rise, glow };
            return SavePrefab(root, RecallMarkPath);
        }

        // ------------------------------------------------------------------
        /// <summary>모래 폭발 프리팹에 지면 균열 룬을 덧붙인다. 폭발 반경 2.5m 기준(지름 5m); 런타임이 실제 반경 비율로 루트를 스케일한다.</summary>
        public static void AddBurstCrack()
        {
            var path = ImpactVfxBuilder.SandBurstPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[VFX] Sand burst missing; build impacts first."); return; }
            var gold = MagicCircleGold();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing = root.transform.Find("Crack");
                var crack = existing != null ? existing.gameObject : Child(root, "Crack");
                var ps = crack.GetComponent<ParticleSystem>();
                ParticleSystemRenderer r;
                if (ps == null) ps = AddSystem(crack, out r); else r = crack.GetComponent<ParticleSystemRenderer>();
                crack.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                var main = ps.main;
                main.duration = 1f;
                main.startLifetime = 0.75f;
                main.startSpeed = 0f;
                main.startSize = 5f;
                main.startColor = Gold;
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 2;
                Burst(ps, 1);
                Size(ps, Punch(0.5f, 1f, 1.05f));
                AlphaFade(ps, Color.white, 0.35f);
                UseBillboard(r, gold, ParticleSystemRenderMode.HorizontalBillboard);
                r.sortingFudge = 8f;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
