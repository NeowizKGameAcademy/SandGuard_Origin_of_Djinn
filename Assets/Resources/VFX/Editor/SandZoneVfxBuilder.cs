using UnityEditor;
using UnityEngine;
using static DesertTower.VFX.Editor.VfxBuildKit;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// 지역 스킬 루프 이펙트. 둘 다 반경 1m 기준으로 만들고(스케일 모드 Hierarchy) 런타임(PlayerSkillCaster)이 실제 반경 비율로 인스턴스를 스케일한다.
    /// 원점은 지면 중심. 지역이 끝나면 방출만 멈추고 남은 입자가 사라진다.
    /// ⑮ 모래 소용돌이 — 바깥에서 중심으로 조여드는 모래 줄기 + 빨려드는 알갱이 + 바닥 먼지.
    /// ⑯ 사막 폭풍 — 가장자리에서 솟는 먼지 벽 + 안을 도는 알갱이·줄기 + 중심 뿌연 안개.
    /// </summary>
    public static class SandZoneVfxBuilder
    {
        public const string VortexPath = PrefabDir + "/VFX_Sand_Vortex.prefab";
        public const string StormPath = PrefabDir + "/VFX_Sand_Storm.prefab";

        [MenuItem("DesertTower/VFX/Build Sand Zones (vortex + storm)")]
        public static void BuildFromMenu() { Build(); EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(VortexPath)); }

        public static void Build() { BuildVortex(); BuildStorm(); }

        static ParticleSystem.MinMaxCurve Range(float min, float max) => new ParticleSystem.MinMaxCurve(min, max);
        static void Flat(ParticleSystem ps) { var shape = ps.shape; shape.rotation = new Vector3(90f, 0f, 0f); } // 원을 눕혀 지면의 고리로
        static void Hierarchy(ParticleSystem.MainModule main) { main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.simulationSpace = ParticleSystemSimulationSpace.Local; }
        static void Orbit(ParticleSystem ps, float orbitMin, float orbitMax, float radialMin, float radialMax, float upMin, float upMax)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = Range(0f, 0f); velocity.y = Range(upMin, upMax); velocity.z = Range(0f, 0f);
            velocity.orbitalX = Range(0f, 0f); velocity.orbitalY = Range(orbitMin, orbitMax); velocity.orbitalZ = Range(0f, 0f);
            velocity.radial = Range(radialMin, radialMax);
        }

        public static GameObject BuildVortex()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Sand_Vortex");
            AddHub(root);

            // 1. 조여드는 모래 줄기: 반경 1 고리에서 나와 빠르게 돌며 중심으로 말려 들어간다.
            var swirl = AddSystem(Child(root, "Swirl"), out var swirlRenderer, loop: true);
            {
                var main = swirl.main; Hierarchy(main);
                main.startLifetime = Range(0.6f, 1f);
                main.startSpeed = Range(0.1f, 0.3f);
                main.startSize = Range(0.05f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(Beige, Beige * 0.7f);
                main.maxParticles = 220;
                Rate(swirl, 70f);
                CircleShape(swirl, 1f, 0.3f); Flat(swirl);
                Orbit(swirl, 6f, 9f, -0.9f, -0.5f, 0.3f, 0.9f);
                Size(swirl, InOut(0.15f, 0.6f));
                AlphaFade(swirl, Color.white, 0.4f);
                UseBillboard(swirlRenderer, s.GlowSoft, ParticleSystemRenderMode.Stretch);
                swirlRenderer.velocityScale = 0.08f; swirlRenderer.lengthScale = 2.5f;
                swirlRenderer.sortingFudge = 2f;
            }

            // 2. 빨려드는 알갱이: 바깥 고리에서 중심으로 굴러 들어오는 큐브.
            var grains = AddSystem(Child(root, "Grains"), out var grainRenderer, loop: true);
            {
                var main = grains.main; Hierarchy(main);
                main.startLifetime = Range(0.5f, 0.9f);
                main.startSpeed = 0f;
                main.startSize = Range(0.03f, 0.06f);
                main.maxParticles = 160;
                RandomRotation3D(main);
                Rate(grains, 45f);
                CircleShape(grains, 1.1f, 0.2f); Flat(grains);
                Orbit(grains, 3f, 5f, -1.8f, -1.2f, 0.05f, 0.25f);
                Tumble(grains, Mathf.PI * 2f);
                ColorRampFade(grains, Beige * 0.5f, Beige * 0.4f, BeigeDark * 0.7f, 0.4f, 0.7f);
                Size(grains, EaseOut(1f, 0.3f));
                UseMesh(grainRenderer, s.Cube, s.CubeWhite);
                grainRenderer.sortingFudge = 1f;
            }

            // 3. 바닥 먼지: 지면을 따라 천천히 도는 연기 조각.
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
            return SavePrefab(root, VortexPath);
        }

        public static GameObject BuildStorm()
        {
            var s = GetShared();
            var root = new GameObject("VFX_Sand_Storm");
            AddHub(root);

            // 1. 먼지 벽: 가장자리 고리에서 솟으며 천천히 도는 연기.
            var wall = AddSystem(Child(root, "Wall"), out var wallRenderer, loop: true);
            {
                var main = wall.main; Hierarchy(main);
                main.startLifetime = Range(1.2f, 1.8f);
                main.startSpeed = Range(0.05f, 0.2f);
                main.startSize = Range(0.5f, 0.8f);
                main.startColor = new Color(0.66f, 0.54f, 0.35f, 0.55f);
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 80;
                Rate(wall, 26f);
                CircleShape(wall, 1f, 0.25f); Flat(wall);
                Orbit(wall, 1.2f, 2f, -0.1f, 0.1f, 0.4f, 0.9f);
                Noise(wall, 0.2f, 0.3f, 0.4f);
                Size(wall, EaseOut(0.6f, 1.3f));
                AlphaFade(wall, Color.white, 0.25f);
                RandomTile(wall, 2, 2);
                UseBillboard(wallRenderer, s.Smoke);
                wallRenderer.sortingFudge = 5f;
            }

            // 2. 안을 도는 알갱이: 원판 전체에서 생겨 빠르게 회전하며 흔들린다.
            var grains = AddSystem(Child(root, "Grains"), out var grainRenderer, loop: true);
            {
                var main = grains.main; Hierarchy(main);
                main.startLifetime = Range(0.8f, 1.2f);
                main.startSpeed = 0f;
                main.startSize = Range(0.03f, 0.06f);
                main.maxParticles = 240;
                RandomRotation3D(main);
                Rate(grains, 80f);
                CircleShape(grains, 1f, 1f); Flat(grains);
                Orbit(grains, 2.5f, 4f, -0.2f, 0.2f, 0.2f, 0.6f);
                Noise(grains, 0.4f, 0.5f, 0.6f);
                Tumble(grains, Mathf.PI * 2f);
                ColorRampFade(grains, Beige * 0.5f, Beige * 0.4f, BeigeDark * 0.7f, 0.4f, 0.7f);
                Size(grains, EaseOut(1f, 0.3f));
                UseMesh(grainRenderer, s.Cube, s.CubeWhite);
                grainRenderer.sortingFudge = 1f;
            }

            // 3. 모래 줄기: 안쪽을 빠르게 도는 스트레치 빌보드.
            var streaks = AddSystem(Child(root, "Streaks"), out var streakRenderer, loop: true);
            {
                var main = streaks.main; Hierarchy(main);
                main.startLifetime = Range(0.5f, 0.8f);
                main.startSpeed = Range(0.1f, 0.2f);
                main.startSize = Range(0.04f, 0.07f);
                main.startColor = new ParticleSystem.MinMaxGradient(Beige, Beige * 0.7f);
                main.maxParticles = 120;
                Rate(streaks, 36f);
                CircleShape(streaks, 0.9f, 0.6f); Flat(streaks);
                Orbit(streaks, 5f, 7f, -0.3f, 0.1f, 0.3f, 1f);
                Size(streaks, InOut(0.15f, 0.6f));
                AlphaFade(streaks, Color.white, 0.4f);
                UseBillboard(streakRenderer, s.GlowSoft, ParticleSystemRenderMode.Stretch);
                streakRenderer.velocityScale = 0.08f; streakRenderer.lengthScale = 2.5f;
                streakRenderer.sortingFudge = 2f;
            }

            // 4. 중심 안개: 크고 느린 뿌연 연기.
            var haze = AddSystem(Child(root, "Haze"), out var hazeRenderer, loop: true);
            {
                var main = haze.main; Hierarchy(main);
                main.startLifetime = Range(1.5f, 2.2f);
                main.startSpeed = Range(0.2f, 0.4f);
                main.startSize = Range(0.9f, 1.3f);
                main.startColor = new Color(0.62f, 0.50f, 0.32f, 0.35f);
                main.startRotation = Range(-Mathf.PI, Mathf.PI);
                main.maxParticles = 24;
                Rate(haze, 6f);
                CircleShape(haze, 0.5f, 1f); Flat(haze);
                Noise(haze, 0.1f, 0.2f, 0.2f);
                Size(haze, EaseOut(0.7f, 1.2f));
                AlphaFade(haze, Color.white, 0.3f);
                RandomTile(haze, 2, 2);
                UseBillboard(hazeRenderer, s.Smoke);
                hazeRenderer.sortingFudge = 6f;
            }
            return SavePrefab(root, StormPath);
        }
    }
}
