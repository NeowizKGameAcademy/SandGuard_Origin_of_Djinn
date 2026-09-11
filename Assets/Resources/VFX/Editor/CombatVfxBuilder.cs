using System.IO;
using DesertTower.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 플레이어·적 근접 전투에 필요한 이펙트 묶음: 플레이어 피격(+화면 비네트), 적 피격 반응, 지팡이 시전(총구),
    /// 이동 계열(점프·공중 점프·착지·발자국), 벽 피격·파괴. 임팩트형은 ImpactSpec으로, 방향성이 있는 것(시전, 벽 피격)과
    /// 루프(발자국), UI(비네트)는 여기서 직접 만든다. 런타임 연결은 VfxHitReaction / VfxCharacterMovement /
    /// VfxFootstepDust / VfxOneShot이 맡고, 데모 프리팹·씬에 꽂는 일은 CombatVfxWiring이 한다.
    /// </summary>
    public static class CombatVfxBuilder
    {
        public const string PlayerHitPath = PrefabDir + "/VFX_Player_Hit.prefab";
        public const string PlayerHitScreenPath = PrefabDir + "/VFX_Player_Hit_Screen.prefab";
        public const string EnemyHitPath = PrefabDir + "/VFX_Enemy_Hit.prefab";
        public const string StaffCastPath = PrefabDir + "/VFX_Staff_Cast.prefab";
        public const string JumpDustPath = PrefabDir + "/VFX_Jump_Dust.prefab";
        public const string AirJumpRingPath = PrefabDir + "/VFX_AirJump_Ring.prefab";
        public const string LandDustPath = PrefabDir + "/VFX_Land_Dust.prefab";
        public const string FootstepSandPath = PrefabDir + "/VFX_Footstep_Sand.prefab";
        public const string WallHitPath = PrefabDir + "/VFX_Wall_Hit.prefab";
        public const string WallDestroyPath = PrefabDir + "/VFX_Wall_Destroy.prefab";
        public const string VignetteTexPath = TexDir + "/T_VFX_Vignette_256.png";

        static readonly Vector3 WallBaseSize = new Vector3(2f, 2f, 0.5f);

        [MenuItem("DesertTower/VFX/Build Combat Set (hit, cast, movement, wall)")]
        public static void BuildFromMenu() { BuildAll(); EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyHitPath)); }

        public static void BuildAll()
        {
            GetShared();
            ImpactVfxBuilder.Build(PlayerHit(), PlayerHitPath);
            BuildPlayerHitScreen();
            ImpactVfxBuilder.Build(EnemyHit(), EnemyHitPath);
            BuildStaffCast();
            ImpactVfxBuilder.Build(JumpDust(), JumpDustPath);
            ImpactVfxBuilder.Build(AirJumpRing(), AirJumpRingPath);
            ImpactVfxBuilder.Build(LandDust(), LandDustPath);
            BuildFootstepSand();
            BuildWallHit();
            ImpactVfxBuilder.Build(WallDestroy(), WallDestroyPath);
            AddWallVolume();
            AssetDatabase.SaveAssets();
        }

        // ---- Impact specs ---------------------------------------------------

        /// <summary>플레이어 피격 (#8): 빨강·흰 작은 파열. 적중점에 스폰. 화면 효과는 VFX_Player_Hit_Screen이 맡는다.</summary>
        public static ImpactSpec PlayerHit() => new ImpactSpec
        {
            PrefabName = "VFX_Player_Hit",
            Scale = 0.8f,
            FlashSize = 1.1f, FlashColor = new Color(1f, 0.7f, 0.6f),
            CoreCount = 3, CoreSize = 0.22f, CoreA = Color.white, CoreB = Red, CoreC = RedDark,
            Shockwave = false,
            DebrisCount = 14,
            DebrisSize = new Vector2(0.05f, 0.1f),
            DebrisSpeed = new Vector2(2f, 4f),
            DebrisLife = new Vector2(0.35f, 0.55f),
            DebrisGravity = 0.6f,
            DebrisA = Color.white, DebrisB = Red, DebrisC = RedDark,
            EmberCount = 6,
            EmberSize = new Vector2(0.1f, 0.18f),
            EmberSpeed = new Vector2(0.4f, 0.9f),
            EmberLife = new Vector2(0.4f, 0.7f),
            EmberColor = Red,
            LightIntensity = 3f, LightRange = 2.5f,
        };

        /// <summary>적 피격 반응 (#8): 흰 플래시 + 틸 마나 스파크 + 캐릭터 색 조각. VfxTint로 조각 색을 바꿀 수 있다.</summary>
        public static ImpactSpec EnemyHit() => new ImpactSpec
        {
            PrefabName = "VFX_Enemy_Hit",
            Scale = 0.7f,
            FlashSize = 1.0f,
            CoreCount = 3, CoreSize = 0.2f, CoreA = Color.white, CoreB = Teal, CoreC = TealDark,
            Shockwave = false,
            DebrisCount = 12,
            DebrisSize = new Vector2(0.06f, 0.12f),
            DebrisSpeed = new Vector2(1.5f, 3f),
            DebrisLife = new Vector2(0.4f, 0.6f),
            DebrisGravity = 0.9f,
            DebrisStart = Beige,
            DebrisA = new Color(0.55f, 0.55f, 0.55f), DebrisB = new Color(0.45f, 0.45f, 0.45f), DebrisC = new Color(0.2f, 0.2f, 0.2f),
            TintableDebris = true,
            EmberCount = 0,
            LightIntensity = 2.5f, LightRange = 2f,
        };

        /// <summary>점프 발밑 먼지: 낮은 모래 큐브 + 작은 먼지 + 베이지 링. 발밑에 스폰.</summary>
        public static ImpactSpec JumpDust() => new ImpactSpec
        {
            PrefabName = "VFX_Jump_Dust",
            GroundCollision = true,
            BodyHeight = 0.12f,
            FlashSize = 0.5f, FlashColor = Beige * 0.6f,
            CoreCount = 0,
            Shockwave = true, ShockwaveSize = 1.2f, ShockwaveColor = Beige,
            DebrisCount = 16,
            DebrisSize = new Vector2(0.04f, 0.09f),
            DebrisSpeed = new Vector2(1.2f, 2.4f),
            DebrisLife = new Vector2(0.4f, 0.7f),
            DebrisGravity = 0.9f,
            DebrisA = Beige * 0.6f, DebrisB = BeigeDark, DebrisC = BeigeDark * 0.6f,
            DebrisShape = DebrisShape.ConeUp, DebrisConeAngle = 75f,
            EmberCount = 0,
            DustCount = 6,
            DustSize = new Vector2(0.3f, 0.5f),
            DustSpeed = new Vector2(0.5f, 1f),
            DustLife = new Vector2(0.5f, 0.9f),
            LightIntensity = 0f,
        };

        /// <summary>#6 더블 점프: 발밑 틸 링 + 큐브 12개 + 작은 플래시. 공중의 발 위치에 스폰.</summary>
        public static ImpactSpec AirJumpRing() => new ImpactSpec
        {
            PrefabName = "VFX_AirJump_Ring",
            FlashSize = 0.8f, FlashColor = new Color(0.75f, 1f, 0.97f),
            CoreCount = 2, CoreSize = 0.18f, CoreA = Color.white, CoreB = Teal, CoreC = Teal,
            Shockwave = true, ShockwaveSize = 1.5f, ShockwaveColor = Teal,
            DebrisCount = 12,
            DebrisSize = new Vector2(0.06f, 0.11f),
            DebrisSpeed = new Vector2(1.5f, 3f),
            DebrisLife = new Vector2(0.4f, 0.6f),
            DebrisGravity = 0.2f,
            DebrisA = Color.white, DebrisB = Teal, DebrisC = TealDark,
            EmberCount = 0,
            LightIntensity = 2.5f, LightRange = 2.5f,
        };

        /// <summary>착지 먼지: 점프 먼지보다 넓고 낮게 퍼진다. 발밑에 스폰.</summary>
        public static ImpactSpec LandDust() => new ImpactSpec
        {
            PrefabName = "VFX_Land_Dust",
            Scale = 1.25f,
            GroundCollision = true,
            BodyHeight = 0.12f,
            FlashSize = 0.5f, FlashColor = Beige * 0.6f,
            CoreCount = 0,
            Shockwave = true, ShockwaveSize = 1.5f, ShockwaveColor = Beige,
            DebrisCount = 22,
            DebrisSize = new Vector2(0.04f, 0.09f),
            DebrisSpeed = new Vector2(1.4f, 2.6f),
            DebrisLife = new Vector2(0.4f, 0.7f),
            DebrisGravity = 1.0f,
            DebrisA = Beige * 0.6f, DebrisB = BeigeDark, DebrisC = BeigeDark * 0.6f,
            DebrisShape = DebrisShape.ConeUp, DebrisConeAngle = 80f,
            EmberCount = 0,
            DustCount = 10,
            DustSize = new Vector2(0.35f, 0.6f),
            DustSpeed = new Vector2(0.6f, 1.2f),
            DustLife = new Vector2(0.6f, 1.0f),
            LightIntensity = 0f,
        };

        /// <summary>
        /// 벽 파괴: 벽 부피가 사암 조각으로 무너지고 먼지가 인다. 기본 상자는 2×2×0.5이고 VfxVolume.Fit()이 실제 벽 크기로 늘린다.
        /// 발밑(경계 상자 바닥 중심)에 스폰. VfxTint로 조각 색을 벽 색에 맞춘다.
        /// </summary>
        public static ImpactSpec WallDestroy() => new ImpactSpec
        {
            PrefabName = "VFX_Wall_Destroy",
            BodyHeight = WallBaseSize.y * 0.5f,
            GroundCollision = true,
            FlashSize = 1.4f, FlashColor = new Color(1f, 0.95f, 0.85f),
            CoreCount = 3, CoreSize = 0.3f, CoreA = Color.white, CoreB = Beige, CoreC = BeigeDark,
            Shockwave = true, ShockwaveSize = 2.6f, ShockwaveColor = Beige, ShockwaveCount = 2, ShockwaveInterval = 0.12f,
            DebrisCount = 28,
            DebrisSize = new Vector2(0.14f, 0.3f),
            DebrisSpeed = new Vector2(0.8f, 2.2f),
            DebrisLife = new Vector2(1.2f, 1.8f),
            DebrisGravity = 1.0f,
            DebrisStart = Beige,
            DebrisA = new Color(0.6f, 0.6f, 0.6f), DebrisB = new Color(0.45f, 0.45f, 0.45f), DebrisC = new Color(0.25f, 0.25f, 0.25f),
            DebrisShape = DebrisShape.Box, DebrisBox = WallBaseSize,
            TintableDebris = true,
            EmberCount = 0,
            DustCount = 14,
            DustSize = new Vector2(0.6f, 1.0f),
            DustSpeed = new Vector2(0.5f, 1.1f),
            DustLife = new Vector2(1.2f, 1.8f),
            LightIntensity = 3f, LightRange = 4f,
        };

        /// <summary>벽 파괴 프리팹에 VfxVolume을 붙여 파편 영역·먼지 반지름·플래시 높이를 벽 크기에 맞출 수 있게 한다.</summary>
        static void AddWallVolume()
        {
            var root = PrefabUtility.LoadPrefabContents(WallDestroyPath);
            try
            {
                var volume = root.GetComponent<VfxVolume>();
                if (volume == null) volume = root.AddComponent<VfxVolume>();
                var debris = root.transform.Find("Debris");
                var dust = root.transform.Find("Dust");
                var flash = root.transform.Find("Flash");
                volume.Systems = new[] { debris != null ? debris.GetComponent<ParticleSystem>() : null, dust != null ? dust.GetComponent<ParticleSystem>() : null, root.GetComponent<ParticleSystem>() };
                volume.Centered = flash != null ? new[] { flash } : new Transform[0];
                volume.BaseSize = WallBaseSize;
                PrefabUtility.SaveAsPrefabAsset(root, WallDestroyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---- Player hit screen (overlay vignette) ---------------------------

        /// <summary>#8 플레이어 피격 화면: 가장자리 빨강 비네트가 0.35초에 걸쳐 사라진다. 위치 무관, 그대로 스폰.</summary>
        static void BuildPlayerHitScreen()
        {
            var texture = BuildVignetteTexture();
            var root = new GameObject("VFX_Player_Hit_Screen");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            root.AddComponent<CanvasGroup>();
            var fade = root.AddComponent<VfxFadeOut>();
            fade.Hold = 0.05f; fade.Duration = 0.3f; fade.StartAlpha = 0.85f;

            var image = Child(root, "Vignette").AddComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(0.9f, 0.15f, 0.1f, 1f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            SavePrefab(root, PlayerHitScreenPath);
        }

        static Texture2D BuildVignetteTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = ((x + 0.5f) / size - 0.5f) * 2f, dy = ((y + 0.5f) / size - 0.5f) * 2f;
                float circle = Mathf.Sqrt(dx * dx + dy * dy);
                float square = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                float d = Mathf.Lerp(circle, square, 0.5f); // 둥근 사각형: 화면 모서리까지 자연스럽게 덮는다
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.05f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            var saved = SavePng(tex, VignetteTexPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(VignetteTexPath);
            importer.filterMode = FilterMode.Bilinear; // 비네트는 부드럽게
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(VignetteTexPath);
        }

        // ---- Staff cast (muzzle) --------------------------------------------

        /// <summary>
        /// 지팡이 시전 (#1 총구 플래시의 지팡이판): 룬 링이 +Z를 향해 펼쳐지고, 틸 고리가 앞으로 퍼지며, 큐브 스파크가 날아간다.
        /// 지팡이 끝에서 발사 방향(+Z)으로 스폰. 마나탄 투사체 자체의 플래시와는 별개로 시전자 쪽에 남는다.
        /// </summary>
        static void BuildStaffCast()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Staff_Cast");
            AddHub(root);
            BuildRune(Child(root, "Rune"), s);
            BuildForwardRing(Child(root, "Ring"), s, Teal, 1.1f, 0.25f);
            BuildCastFlash(Child(root, "Flash"), s, 0.8f, new Color(0.8f, 1f, 0.98f), 3f, 2.5f);
            BuildForwardSparks(Child(root, "Sparks"), s, 10, new Vector2(3f, 6f), new Vector2(0.05f, 0.09f), 25f, Color.white, Teal, TealDark, 0.3f, 0.35f);
            SavePrefab(root, StaffCastPath);
        }

        // Magic-circle quad facing +Z, spinning, growing then fading. Local space so it stays on the staff.
        static void BuildRune(GameObject go, Shared s)
        {
            var ps = AddSystem(go, out var r);
            var main = ps.main;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.32f;
            main.startSpeed = 0f;
            main.startSize = FitScale(s.Quad, 0.9f);
            main.startColor = Teal;
            main.maxParticles = 2;
            Burst(ps, 1);
            Size(ps, EaseOut(0.3f, 1f));
            AlphaFade(ps, Color.white, 0.4f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = 0f; rot.y = 0f;
            rot.z = Mathf.PI * 1.5f;
            UseMesh(r, s.Quad, s.MagicCircleTeal);
            r.alignment = ParticleSystemRenderSpace.Local; // quad plane = local XY, normal = +Z
            go.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        }

        // Torus laid on the XY plane so it expands along +Z.
        static void BuildForwardRing(GameObject go, Shared s, Color color, float size, float life)
        {
            var ps = AddSystem(go, out var r);
            var main = ps.main;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = life;
            main.startSpeed = 2.5f;
            main.startSize = FitScale(s.Torus, size);
            main.startColor = color;
            main.maxParticles = 2;
            var euler = AxisToForward(ThinAxis(s.Torus)).eulerAngles * Mathf.Deg2Rad;
            main.startRotation3D = true;
            main.startRotationX = euler.x; main.startRotationY = euler.y; main.startRotationZ = euler.z;
            Burst(ps, 1);
            ConeShape(ps, 0f, 0.001f); // emits along local +Z
            Size(ps, EaseOut(0.15f, 1f));
            AlphaFade(ps, Color.white, 0.3f);
            UseMesh(r, s.Torus, s.MeshAdditive);
            r.alignment = ParticleSystemRenderSpace.Local;
        }

        static void BuildCastFlash(GameObject go, Shared s, float size, Color color, float lightIntensity, float lightRange)
        {
            var ps = AddSystem(go, out var r);
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 2;
            Burst(ps, 1);
            Size(ps, HoldThenDrop(0.4f));
            UseBillboard(r, s.GlowWhite);
            r.sortingFudge = -10f;
            if (lightIntensity > 0f) ParticleLight(ps, s.LightPrefab, lightIntensity, lightRange, ratio: 1f);
        }

        static ParticleSystem BuildForwardSparks(GameObject go, Shared s, int count, Vector2 speed, Vector2 size, float angle,
            Color a, Color b, Color c, float lifeMin, float lifeMax)
        {
            var ps = AddSystem(go, out var r);
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.maxParticles = count + 4;
            RandomRotation3D(main);
            Burst(ps, count);
            ConeShape(ps, angle, 0.05f);
            Drag(ps, 0.3f, 2f);
            Tumble(ps);
            Size(ps, Linear(1f, 0f));
            ColorRamp(ps, a, b, c, 0.3f);
            UseMesh(r, s.Cube, s.CubeWhite);
            return ps;
        }

        // ---- Footstep sand (loop, rate over distance) ------------------------

        /// <summary>#24 발밑 먼지 트레일: 움직인 거리마다 작은 먼지를 남긴다. 발 위치에 자식으로 붙이고 VfxFootstepDust가 켜고 끈다.</summary>
        static void BuildFootstepSand()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Footstep_Sand");
            AddHub(root);
            root.AddComponent<VfxFootstepDust>();

            var puffs = AddSystem(Child(root, "Puffs"), out var pr, loop: true);
            var pm = puffs.main;
            pm.duration = 1f;
            pm.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            pm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            pm.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            pm.startColor = new Color(0.62f, 0.50f, 0.32f, 0.7f);
            pm.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            pm.maxParticles = 24;
            Rate(puffs, perSecond: 0f, perUnit: 2.2f);
            ConeShape(puffs, 40f, 0.12f);
            puffs.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // up
            Size(puffs, EaseOut(0.5f, 1.3f));
            AlphaFade(puffs, Color.white, 0.2f);
            RandomTile(puffs, 2, 2);
            UseBillboard(pr, s.Smoke);
            pr.sortingFudge = 5f;

            SavePrefab(root, FootstepSandPath);
        }

        // ---- Wall hit (directional) -----------------------------------------

        /// <summary>벽 피격: 표면 바깥(+Z)으로 튀는 사암 조각 + 먼지 + 작은 플래시. 적중점에서 표면 법선 방향으로 스폰.</summary>
        static void BuildWallHit()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Wall_Hit");
            AddHub(root);
            BuildCastFlash(Child(root, "Flash"), s, 0.6f, new Color(1f, 0.93f, 0.8f), 0f, 0f);

            var chips = BuildForwardSparks(Child(root, "Chips"), s, 12, new Vector2(1.8f, 3.6f), new Vector2(0.05f, 0.1f), 40f,
                new Color(0.6f, 0.6f, 0.6f), new Color(0.45f, 0.45f, 0.45f), new Color(0.25f, 0.25f, 0.25f), 0.4f, 0.7f);
            var cm = chips.main;
            cm.startColor = Beige;
            cm.gravityModifier = 0.8f;
            Size(chips, HoldThenDrop(0.6f));
            root.AddComponent<VfxTint>().Systems = new[] { chips };

            var dust = AddSystem(Child(root, "Dust"), out var dr);
            var dm = dust.main;
            dm.duration = 1f;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            dm.startColor = new Color(0.62f, 0.50f, 0.32f, 0.8f);
            dm.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            dm.maxParticles = 8;
            Burst(dust, 5);
            ConeShape(dust, 45f, 0.08f);
            Drag(dust, 0.3f, 1f);
            Size(dust, EaseOut(0.5f, 1.3f));
            AlphaFade(dust, Color.white, 0.25f);
            RandomTile(dust, 2, 2);
            UseBillboard(dr, s.Smoke);
            dr.sortingFudge = 5f;

            SavePrefab(root, WallHitPath);
        }
    }
}
