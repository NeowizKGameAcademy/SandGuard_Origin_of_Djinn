using UnityEditor;
using UnityEngine;

namespace DesertTower.VFX.Editor
{
    /// <summary>
    /// Builds every code-defined VFX prefab in one go. Menu: DesertTower > VFX > Build All.
    /// Batch: <c>Unity -batchmode -projectPath . -executeMethod DesertTower.VFX.Editor.VfxBatch.BuildAll -quit</c>
    /// (add <c>BuildAllAndRender</c> instead of <c>BuildAll</c> to also write preview frames; needs graphics).
    /// </summary>
    public static class VfxBatch
    {
        [MenuItem("DesertTower/VFX/Build All", priority = -100)]
        public static void BuildAll()
        {
            VfxBuildKit.BuildShared();
            foreach (var (spec, path) in ImpactVfxBuilder.All()) ImpactVfxBuilder.Build(spec, path);
            ManaBoltProjectileBuilder.Build();
            ExperienceMoteBuilder.Build();
            CoreAmbientBuilder.Build();
            FlameBreathBuilder.BuildBreath();
            FlameBreathBuilder.BuildBurning();
            GoldVfxBuilder.BuildLevelUp();
            GoldVfxBuilder.BuildWaveClear();
            LoopVfxBuilder.BuildSummonCircle();
            LoopVfxBuilder.BuildManaCharge();
            LoopVfxBuilder.BuildTorch();
            LoopVfxBuilder.BuildFloatingDust();
            BeamVfxBuilder.BuildPierceBeam();
            BeamVfxBuilder.BuildPierceBeamNoise();
            BeamVfxBuilder.BuildSummonPillar();
            CombatVfxBuilder.BuildAll();
            UpdraftVfxBuilder.Build();
            SandZoneVfxBuilder.Build();
            SkillVfxBuilder.Build(); // 관통탄 충전·흔적 표식 + 모래 폭발 균열(임팩트 다음)
            RequestedVfxBuilder.Build();
            CoreDestructionBuilder.Build();
            CobraDestructionBuilder.Build();
            AssetDatabase.SaveAssets();
            VfxShowcaseBuilder.Build();
            // 프리팹을 다시 만들면 소리 이미터가 사라지므로 오디오 배선을 다시 건다 (SfxEmitter 자식, 큐 참조만).
            SandGuard.Audio.Editor.AudioWiring.WireVfxPrefabs();
            Debug.Log("[VFX] Build All done.");
        }

        public static void BuildAllAndRender()
        {
            BuildAll();
            VfxPreviewRenderer.RenderAll();
            VfxShowcaseBuilder.RenderCheck(VfxPreviewRenderer.OutputDir());
        }
    }
}
