using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 지역 스킬 연출.
    /// ⑮ 모래 소용돌이 — 반경 1m 기준(스케일 모드 Hierarchy, 런타임이 실제 반경 비율로 스케일). 위로 갈수록 좁아지는 금빛 나선 기둥(높이 약 3m):
    ///    조여들며 솟는 줄기 + 층층이 떠오르는 링 + 빨려 올라가는 알갱이·바위 + 바닥 먼지. 끝날 때 "OnEnd*" 자식(붕괴 버스트·튀는 링)이 재생된다(PlayerSandZone.End).
    /// ⑯ 사막 폭풍 — 스케일 1(월드 단위). 코어에서 바깥으로 퍼지는 모래 벽 링. 루트의 <see cref="VfxStormFront"/>가 매 프레임 링 반경을 받아
    ///    벽(봉인 폭풍 셰이더를 두른 원통 껍질)을 키우고 파티클의 원형 방출 반경·방출량을 따라 옮긴다. 시작점에는 금빛 플래시·링·기둥이 한 번 터진다.
    /// </summary>
    public static class SandZoneVfxBuilder
    {
        public const string VortexPath = PrefabDir + "/VFX_Sand_Vortex.prefab";
        public const string StormPath = PrefabDir + "/VFX_Sand_Storm.prefab";
        const string StormBodyMaterialPath = "Assets/Resources/VFX/TempleSandstorm/M_StormBody_0.mat";
        // 신전 봉인 폭풍(Level.unity)이 쓰는 벽 머티리얼. 이걸 복사해 살짝 투명하게 만든 것이 스킬 폭풍의 벽이다 → 같은 모래가 코어에서 밀려 나가는 것처럼 보인다.
        static readonly string[] LevelStormMaterialPaths =
        {
            "Assets/TempleArt/ArchitectureV2/Environment/LevelStorm_Sand_wall_layer_1.mat",
            "Assets/TempleArt/ArchitectureV2/Environment/LevelStorm_Sand_wall_layer_2.mat",
            "Assets/TempleArt/ArchitectureV2/Environment/LevelStorm_Sand_wall_layer_3.mat",
        };
        static readonly string[] FallbackStormMaterialPaths =
        {
            "Assets/Resources/VFX/TempleSandstorm/M_StormBody_0.mat",
            "Assets/Resources/VFX/TempleSandstorm/M_StormBody_1.mat",
            "Assets/Resources/VFX/TempleSandstorm/M_StormBody_2.mat",
        };
        // 코어는 신전 꼭대기(y≈54)에 있으므로 벽은 시작점 아래 62m(마당 바닥 아래)부터 위 14m까지 세운다 → 신전 전체를 바닥부터 꼭대기까지 훑는다.
        public const float StormWallBottom = -62f, StormWallHeight = 76f;

        [MenuItem("DesertTower/VFX/Build Sand Zones (vortex + storm)")]
        public static void BuildFromMenu() { Build(); EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(VortexPath)); }

        public static void Build() { BuildVortex(); BuildStorm(); }

        static ParticleSystem.MinMaxCurve Range(float min, float max) => new ParticleSystem.MinMaxCurve(min, max);
        static void Flat(ParticleSystem ps) { var shape = ps.shape; shape.rotation = new Vector3(90f, 0f, 0f); } // 원을 눕혀 지면의 고리로
        static void Hierarchy(ParticleSystem.MainModule main) { main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.simulationSpace = ParticleSystemSimulationSpace.Local; }
        static void Orbit(ParticleSystem ps, float orbitMin, float orbitMax, float radialMin, float radialMax, float upMin, float upMax, ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.Local)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true; velocity.space = space;
            velocity.x = Range(0f, 0f); velocity.y = Range(upMin, upMax); velocity.z = Range(0f, 0f);
            velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(orbitMin, orbitMax); velocity.orbitalZ = Range(0f, 0f);
            velocity.radial = Range(radialMin, radialMax);
        }
        static void HorizontalRing(ParticleSystem.MainModule main, Mesh torus)
        {
            var euler = AxisToUp(ThinAxis(torus)).eulerAngles * Mathf.Deg2Rad;
            main.startRotation3D = true;
            main.startRotationX = euler.x; main.startRotationY = euler.y; main.startRotationZ = euler.z;
        }

        // ==================================================================
        public static GameObject BuildVortex()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Sand_Vortex");
            AddHub(root);

            // 1. 조여들며 솟는 모래 줄기: 바닥 고리에서 나와 빠르게 돌며 위로 올라가고 중심으로 말려 들어간다 → 원뿔.
            var swirl = AddSystem(Child(root, "Swirl"), out var swirlRenderer, loop: true);
            {
                var main = swirl.main; Hierarchy(main);
                main.startLifetime = Range(1f, 1.3f);
                main.startSpeed = Range(0.1f, 0.3f);
                main.startSize = Range(0.05f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(Gold, Beige);
                main.maxParticles = 260;
                Rate(swirl, 90f);
                CircleShape(swirl, 1f, 0.25f); Flat(swirl);
                Orbit(swirl, 7f, 10f, -0.55f, -0.3f, 1.6f, 2.4f);
                Size(swirl, InOut(0.15f, 0.65f));
                AlphaFade(swirl, Color.white, 0.45f);
                UseBillboard(swirlRenderer, s.GlowSoft, ParticleSystemRenderMode.Stretch);
                swirlRenderer.velocityScale = 0.08f; swirlRenderer.lengthScale = 2.5f;
                swirlRenderer.sortingFudge = 2f;
            }

            // 2. 층층이 떠오르는 금빛 링: 바닥에서 지름 2로 생겨 3m를 오르며 0.5로 좁아진다.
            var rings = AddSystem(Child(root, "Rings"), out var ringRenderer, loop: true);
            {
                var main = rings.main; Hierarchy(main);
                main.startLifetime = 1.15f;
                main.startSpeed = 0f;
                main.startColor = new Color(Gold.r, Gold.g, Gold.b, 0.8f);
                main.startSize = FitScale(s.Torus, 2f);
                main.maxParticles = 8;
                HorizontalRing(main, s.Torus);
                Rate(rings, 3.5f);
                var velocity = rings.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = Range(0f, 0f); velocity.y = Range(2.5f, 2.7f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(0f, 0f); velocity.orbitalZ = Range(0f, 0f);
                velocity.radial = Range(0f, 0f);
                Size(rings, EaseOut(1f, 0.25f));
                AlphaFade(rings, Color.white, 0.55f);
                UseMesh(ringRenderer, s.Torus, s.MeshAdditive);
                ringRenderer.sortingFudge = 3f;
            }

            // 3. 빨려 올라가는 알갱이: 바깥 고리에서 돌며 안쪽·위로.
            var grains = AddSystem(Child(root, "Grains"), out var grainRenderer, loop: true);
            {
                var main = grains.main; Hierarchy(main);
                main.startLifetime = Range(0.9f, 1.3f);
                main.startSpeed = 0f;
                main.startSize = Range(0.04f, 0.09f);
                main.maxParticles = 200;
                RandomRotation3D(main);
                Rate(grains, 60f);
                CircleShape(grains, 1.1f, 0.2f); Flat(grains);
                Orbit(grains, 4f, 6f, -1.5f, -0.9f, 0.8f, 1.8f);
                Tumble(grains, Mathf.PI * 2f);
                ColorRampFade(grains, Gold * 0.6f, Beige * 0.45f, BeigeDark * 0.7f, 0.4f, 0.7f);
                Size(grains, EaseOut(1f, 0.3f));
                UseMesh(grainRenderer, s.Cube, s.CubeWhite);
                grainRenderer.sortingFudge = 1f;
            }

            // 4. 바위: 큼직한 조각 몇 개가 느리게 돌며 떠오른다(컨셉의 튀는 바위).
            var rocks = AddSystem(Child(root, "Rocks"), out var rockRenderer, loop: true);
            {
                var main = rocks.main; Hierarchy(main);
                main.startLifetime = Range(1.1f, 1.5f);
                main.startSpeed = 0f;
                main.startSize = Range(0.12f, 0.2f);
                main.maxParticles = 30;
                RandomRotation3D(main);
                Rate(rocks, 8f);
                CircleShape(rocks, 0.9f, 0.3f); Flat(rocks);
                Orbit(rocks, 3f, 4.5f, -0.6f, -0.3f, 0.7f, 1.4f);
                Tumble(rocks, Mathf.PI);
                ColorRampFade(rocks, Beige * 0.4f, BeigeDark * 0.8f, BeigeDark * 0.5f, 0.3f, 0.75f);
                Size(rocks, EaseOut(1f, 0.6f));
                UseMesh(rockRenderer, s.Cube, s.CubeWhite);
                rockRenderer.sortingFudge = 1f;
            }

            // 5. 바닥 먼지: 지면을 따라 천천히 도는 연기 조각.
            var dust = AddSystem(Child(root, "Dust"), out var dustRenderer, loop: true);
            {
                var main = dust.main; Hierarchy(main);
                main.startLifetime = Range(0.8f, 1.2f);
                main.startSpeed = Range(0.1f, 0.3f);
                main.startSize = Range(0.35f, 0.6f);
                main.startColor = new Color(0.62f, 0.50f, 0.32f, 0.5f);
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 40;
                Rate(dust, 14f);
                CircleShape(dust, 0.8f, 0.8f); Flat(dust);
                Orbit(dust, 1.2f, 2f, -0.3f, -0.1f, 0.1f, 0.25f);
                Noise(dust, 0.15f, 0.3f, 0.3f);
                Size(dust, EaseOut(0.5f, 1.2f));
                AlphaFade(dust, Color.white, 0.2f);
                RandomTile(dust, 2, 2);
                UseBillboard(dustRenderer, s.Smoke);
                dustRenderer.sortingFudge = 5f;
            }

            // 6. 끝: 붕괴 버스트 — 모인 것이 위로 튀어 오른다(적 쳐올림과 같은 순간). 이름이 OnEnd로 시작하면 PlayerSandZone.End가 재생한다.
            var collapse = AddSystem(Child(root, "OnEnd Collapse"), out var collapseRenderer);
            {
                var main = collapse.main; Hierarchy(main); // playOnAwake는 계층 공유 설정이라 끄지 않는다(끄면 소용돌이 전체가 안 돈다). 스폰 직후 PlayerSkillCaster.Zone이 OnEnd*를 멈춘다
                main.duration = 1f;
                main.startLifetime = Range(0.6f, 0.9f);
                main.startSpeed = Range(4f, 7f);
                main.startSize = Range(0.05f, 0.11f);
                main.maxParticles = 60;
                main.gravityModifier = 0.8f;
                RandomRotation3D(main);
                Burst(collapse, 44);
                ConeShape(collapse, 14f, 0.4f);
                collapse.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 원뿔이 위를 본다
                Tumble(collapse, Mathf.PI * 2f);
                ColorRamp(collapse, Color.white, Gold, GoldDark, 0.3f);
                Size(collapse, HoldThenDrop(0.6f));
                UseMesh(collapseRenderer, s.Cube, s.CubeWhite);
                ParticleLight(collapse, s.LightPrefab, 3f, 4f, ratio: 0.05f);
            }
            var pop = AddSystem(Child(root, "OnEnd Ring"), out var popRenderer);
            {
                var main = pop.main; Hierarchy(main);
                main.duration = 1f;
                main.startLifetime = 0.4f;
                main.startSpeed = 0f;
                main.startColor = Gold;
                main.startSize = FitScale(s.Torus, 2.4f);
                main.maxParticles = 2;
                HorizontalRing(main, s.Torus);
                Burst(pop, 1);
                var velocity = pop.velocityOverLifetime;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = Range(0f, 0f); velocity.y = Range(3f, 3f); velocity.z = Range(0f, 0f);
                velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(0f, 0f); velocity.orbitalZ = Range(0f, 0f);
                velocity.radial = Range(0f, 0f);
                Size(pop, EaseOut(0.5f, 1f));
                AlphaFade(pop, Color.white, 0.3f);
                UseMesh(popRenderer, s.Torus, s.MeshAdditive);
            }
            return SavePrefab(root, VortexPath);
        }

        // ==================================================================
        public static GameObject BuildStorm()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Sand_Storm");
            AddHub(root);
            var front = root.AddComponent<VfxStormFront>();

            // 1. 모래 벽: 신전 봉인 폭풍과 같은 3겹 벽(반지름 1 기준, 높이는 m 단위 그대로). 래퍼의 XZ 스케일 = 링 반경(VfxStormFront가 갱신).
            //    바닥 아래 6m부터 위 48m까지 솟아 신전 꼭대기까지 훑고, 위로 갈수록 바깥으로 살짝 기운다.
            var wall = Child(root, "Wall");
            var layers = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                var layerGo = Child(wall, "Sand wall layer " + (i + 1));
                layerGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(StormWallMesh("Skill storm wall " + i, 1f + 0.03f * i, StormWallBottom, StormWallHeight, 0.05f + 0.02f * i, i * 0.8f, 0.03f), MeshDir + "/SkillStorm_Wall_" + i + ".asset");
                var r = layerGo.AddComponent<MeshRenderer>();
                r.sharedMaterial = SkillStormMaterial(i);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                layers[i] = r;
            }
            var flow = wall.AddComponent<TempleSandstorm>();
            flow.sandLayers = layers;
            flow.animationSpeed = 2f;

            // 1a. 낮은 띠: 마당 바닥 높이(시작점 아래 52m)에서 먼지가 흘러 바닥층을 훑는 느낌을 준다.
            var lowDust = AddSystem(Child(root, "FrontDustLow"), out var lowDustRenderer, loop: true);
            {
                lowDust.transform.localPosition = new Vector3(0f, -52f, 0f);
                var main = lowDust.main;
                main.startLifetime = Range(1.6f, 2.4f);
                main.startSpeed = 0f;
                main.startSize = Range(3f, 5f);
                main.startColor = new Color(0.66f, 0.54f, 0.35f, 0.5f);
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 800;
                Rate(lowDust, 6f);
                CircleShape(lowDust, 1f, 0.15f); Flat(lowDust);
                Orbit(lowDust, 0f, 0f, 2f, 4f, 1.5f, 5f, ParticleSystemSimulationSpace.World);
                Noise(lowDust, 0.5f, 0.2f, 0.4f);
                Size(lowDust, EaseOut(0.6f, 1.4f));
                AlphaFade(lowDust, Color.white, 0.25f);
                RandomTile(lowDust, 2, 2);
                UseBillboard(lowDustRenderer, s.Smoke);
                lowDustRenderer.sortingFudge = 5f;
            }

            // 1b. 높은 띠: 벽 위쪽(y 8)에서도 먼지가 흘러 꼭대기를 훑는 느낌을 준다.
            var highDust = AddSystem(Child(root, "FrontDustHigh"), out var highDustRenderer, loop: true);
            {
                highDust.transform.localPosition = new Vector3(0f, 8f, 0f);
                var main = highDust.main;
                main.startLifetime = Range(1.6f, 2.4f);
                main.startSpeed = 0f;
                main.startSize = Range(3f, 5f);
                main.startColor = new Color(0.66f, 0.54f, 0.35f, 0.45f);
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 800;
                Rate(highDust, 5f);
                CircleShape(highDust, 1f, 0.15f); Flat(highDust);
                Orbit(highDust, 0f, 0f, 2f, 4f, 2f, 6f, ParticleSystemSimulationSpace.World);
                Noise(highDust, 0.5f, 0.2f, 0.4f);
                Size(highDust, EaseOut(0.6f, 1.4f));
                AlphaFade(highDust, Color.white, 0.25f);
                RandomTile(highDust, 2, 2);
                UseBillboard(highDustRenderer, s.Smoke);
                highDustRenderer.sortingFudge = 5f;
            }

            // 2. 링을 따라가는 먼지 벽: 원형 가장자리에서 솟으며 바깥으로 흐른다. 월드 공간이라 링 뒤에 꼬리가 남는다.
            var dust = AddSystem(Child(root, "FrontDust"), out var dustRenderer, loop: true);
            {
                var main = dust.main;
                main.startLifetime = Range(1.4f, 2f);
                main.startSpeed = 0f;
                main.startSize = Range(1.2f, 2.2f);
                main.startColor = new Color(0.66f, 0.54f, 0.35f, 0.6f);
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 1600;
                Rate(dust, 10f);
                CircleShape(dust, 1f, 0.15f); Flat(dust);
                Orbit(dust, 0f, 0f, 2f, 4f, 1.5f, 3f, ParticleSystemSimulationSpace.World);
                Noise(dust, 0.3f, 0.3f, 0.5f);
                Size(dust, EaseOut(0.6f, 1.4f));
                AlphaFade(dust, Color.white, 0.25f);
                RandomTile(dust, 2, 2);
                UseBillboard(dustRenderer, s.Smoke);
                dustRenderer.sortingFudge = 5f;
            }

            // 3. 튀는 알갱이: 링에서 바깥·위로 던져져 떨어진다.
            var grains = AddSystem(Child(root, "FrontGrains"), out var grainRenderer, loop: true);
            {
                var main = grains.main;
                main.startLifetime = Range(0.9f, 1.3f);
                main.startSpeed = 0f;
                main.startSize = Range(0.06f, 0.14f);
                main.maxParticles = 900;
                main.gravityModifier = 1f;
                RandomRotation3D(main);
                Rate(grains, 14f);
                CircleShape(grains, 1f, 0.1f); Flat(grains);
                Orbit(grains, 0f, 0f, 5f, 9f, 2f, 5f, ParticleSystemSimulationSpace.World);
                Tumble(grains, Mathf.PI * 2f);
                ColorRampFade(grains, Gold * 0.5f, Beige * 0.45f, BeigeDark * 0.7f, 0.35f, 0.7f);
                Size(grains, EaseOut(1f, 0.4f));
                UseMesh(grainRenderer, s.Cube, s.CubeWhite);
                grainRenderer.sortingFudge = 1f;
            }

            // 4. 지면 줄기: 바깥으로 쏘아지는 모래 스트레치 빌보드.
            var streaks = AddSystem(Child(root, "FrontStreaks"), out var streakRenderer, loop: true);
            {
                var main = streaks.main;
                main.startLifetime = Range(0.5f, 0.8f);
                main.startSpeed = 0f;
                main.startSize = Range(0.06f, 0.1f);
                main.startColor = new ParticleSystem.MinMaxGradient(Gold, Beige * 0.8f);
                main.maxParticles = 600;
                Rate(streaks, 12f);
                CircleShape(streaks, 1f, 0.1f); Flat(streaks);
                Orbit(streaks, 0f, 0f, 8f, 12f, 0.3f, 1.2f, ParticleSystemSimulationSpace.World);
                Size(streaks, InOut(0.15f, 0.6f));
                AlphaFade(streaks, Color.white, 0.4f);
                UseBillboard(streakRenderer, s.GlowSoft, ParticleSystemRenderMode.Stretch);
                streakRenderer.velocityScale = 0.06f; streakRenderer.lengthScale = 2.5f;
                streakRenderer.sortingFudge = 2f;
            }

            // 5. 시작점(코어): 금빛 플래시 + 링 두 겹 + 솟는 기둥 + 라이트. 한 번.
            var flash = AddSystem(Child(root, "Origin Flash"), out var flashRenderer);
            {
                flash.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                var main = flash.main;
                main.duration = 1f;
                main.startLifetime = 0.3f;
                main.startSpeed = 0f;
                main.startSize = 4f;
                main.startColor = new Color(1f, 0.93f, 0.7f);
                main.maxParticles = 2;
                Burst(flash, 1);
                Size(flash, HoldThenDrop(0.3f));
                UseBillboard(flashRenderer, s.GlowWhite);
                flashRenderer.sortingFudge = -10f;
                ParticleLight(flash, s.LightPrefab, 7f, 14f, ratio: 1f);
            }
            var originRings = AddSystem(Child(root, "Origin Rings"), out var originRingRenderer);
            {
                originRings.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                var main = originRings.main;
                main.duration = 1f;
                main.startLifetime = 0.6f;
                main.startSpeed = 0f;
                main.startColor = Gold;
                main.startSize = FitScale(s.Torus, 12f);
                main.maxParticles = 4;
                HorizontalRing(main, s.Torus);
                Bursts(originRings, 1, 2, 0.14f);
                Size(originRings, EaseOut(0.08f, 1f));
                AlphaFade(originRings, Color.white, 0.3f);
                UseMesh(originRingRenderer, s.Torus, s.MeshAdditive);
            }
            var column = AddSystem(Child(root, "Origin Column"), out var columnRenderer);
            {
                var main = column.main;
                main.duration = 1f;
                main.startLifetime = Range(0.8f, 1.2f);
                main.startSpeed = Range(6f, 10f);
                main.startSize = Range(0.1f, 0.2f);
                main.maxParticles = 50;
                main.gravityModifier = 0.5f;
                RandomRotation3D(main);
                Burst(column, 40);
                ConeShape(column, 8f, 0.6f);
                column.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Tumble(column, Mathf.PI * 2f);
                ColorRamp(column, Color.white, Gold, GoldDark, 0.3f);
                Size(column, HoldThenDrop(0.6f));
                UseMesh(columnRenderer, s.Cube, s.CubeWhite);
            }

            front.ringScaled = new[] { wall.transform };
            front.ringRenderers = layers;
            front.wallFlow = flow;
            front.frontEmitters = new[] { dust, lowDust, highDust, grains, streaks };
            // 무늬 폭 24m·접선 7m/s는 셰이더 스크롤(무늬 하나가 3.4초마다 지나간다)과 맞춘 값이다. 반경 90m에서도 벽과 먼지가 같은 속도로 흐른다.
            front.patternWidth = 24f; front.minTiles = 8f; front.orbitSpeed = 7f; front.spinKeepsAngularSpeed = 0.85f;
            front.fadeStart = 0.8f; front.fadeEnd = 0f;   // 반경 72m(약 5.1초)부터 1.3초에 걸쳐 흩어진다 → 최대 반경에서 멈춘 채 끝나지 않는다
            front.maxRate = 900f;   // 반경 90m(둘레 565m)에서 미터당 1.6개. maxParticles 상한과 맞춘 값이다.
            front.selfExpandSpeed = 6f; front.selfMaxRadius = 12f; // 미리보기·쇼케이스용. 런타임(PlayerSandStorm)이 SetFront를 부르면 무시된다
            front.SetFront(0.01f, 4f, 12f);
            return SavePrefab(root, StormPath);
        }

        const string MeshDir = RootDir + "/Meshes";

        /// <summary>Level 폭풍 벽 머티리얼의 복사본(살짝 투명, 안쪽 층을 나중에 그림). 원본이 없으면 프로토타입 StormBody를 쓴다.</summary>
        static Material SkillStormMaterial(int i)
        {
            string path = MatDir + "/M_VFX_SkillStorm_" + i + ".mat";
            var src = AssetDatabase.LoadAssetAtPath<Material>(LevelStormMaterialPaths[i]) ?? AssetDatabase.LoadAssetAtPath<Material>(FallbackStormMaterialPaths[i]);
            if (src == null) return GetShared().Smoke;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(src); AssetDatabase.CreateAsset(mat, path); }
            else mat.CopyPropertiesFromMaterial(src);
            mat.shader = src.shader;
            if (mat.HasProperty("_Opacity")) mat.SetFloat("_Opacity", 0.6f);          // 지나가는 파도라 신전이 비쳐 보이게
            if (mat.HasProperty("_DensityFloor")) mat.SetFloat("_DensityFloor", 0.15f + 0.08f * i);
            if (mat.HasProperty("_WaveAmplitude")) mat.SetFloat("_WaveAmplitude", 0.35f);
            if (mat.HasProperty("_Swirl")) mat.SetFloat("_Swirl", 0.35f);
            if (mat.HasProperty("_FlowSpeed")) mat.SetFloat("_FlowSpeed", 1.6f);
            mat.renderQueue = 3002 - i;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>반지름 radius(스케일 전 단위)인 원형 벽. yBottom에서 height(m)만큼 솟고 위로 갈수록 lean(반지름 비율)만큼 바깥으로 기운다. uv = (둘레, 높이) — 봉인 폭풍 벽과 같은 규약.</summary>
        static Mesh StormWallMesh(string name, float radius, float yBottom, float height, float lean, float phase, float ripple)
        {
            const int rings = 128, rows = 16;
            var v = new Vector3[(rings + 1) * (rows + 1)]; var uv = new Vector2[v.Length]; var tri = new int[rings * rows * 6]; int k = 0;
            for (int a = 0; a <= rings; a++)
                for (int b = 0; b <= rows; b++)
                {
                    float u = a / (float)rings, t = b / (float)rows, theta = u * Mathf.PI * 2f;
                    float bulge = Mathf.Sin(t * Mathf.PI) * ripple * (Mathf.Sin(theta * 5f + phase) + 0.6f * Mathf.Sin(theta * 9f + phase * 2f));
                    float r = radius + lean * t * t + bulge;
                    float y = yBottom + t * height * (1f + 0.05f * Mathf.Sin(theta * 4f + phase));
                    int index = a * (rows + 1) + b;
                    v[index] = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r); uv[index] = new Vector2(u, t);
                    if (a < rings && b < rows) { int q = index; tri[k++] = q; tri[k++] = q + rows + 1; tri[k++] = q + 1; tri[k++] = q + 1; tri[k++] = q + rows + 1; tri[k++] = q + rows + 2; }
                }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, uv = uv, triangles = tri };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(4f); mesh.bounds = bounds; // 스케일·기울기 여유
            return mesh;
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                old.Clear(); old.indexFormat = mesh.indexFormat; old.name = mesh.name;
                old.vertices = mesh.vertices; old.uv = mesh.uv; old.normals = mesh.normals; old.triangles = mesh.triangles; old.bounds = mesh.bounds;
                old.UploadMeshData(false); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(old); return old;
            }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
