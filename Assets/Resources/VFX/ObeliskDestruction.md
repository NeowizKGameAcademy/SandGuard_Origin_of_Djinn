# Obelisk destruction / blue partial explosion

- `Prefabs/VFX_Blue_Partial_Explosion.prefab`: reusable, non-looping blue explosion at its local origin. Ice-blue flash, blue cube sparks, expanding ring, pale blue smoke. Spawn with `PrefabPool.Spawn` and release after 1.5 seconds; size is controlled by transform scale.
- `Prefabs/VFX_Obelisk_Destruction.prefab`: four partial explosions at 0.04 / 0.21 / 0.38 / 0.55 seconds, followed by a larger burst and mesh breakup at 0.72 seconds. The original textured meshes become falling fragments; rubble shrinks away from 2.8 to 3.8 seconds. Pool lifetime: 4 seconds.
- The build-catalog prefab `Assets/2.Model/Prefabs/Tower_Obelisk.prefab` is wired through `VfxDestructionOnDeath` on the same object as `TowerHealth`. The detached effect continues after the gameplay tower is removed.
- `VfxObeliskDestruction.BindTarget` hides the original body, lights and particles. It never re-enables the destroyed tower automatically. `RestoreTarget` is reserved for previews or explicit respawn.

Rebuild and wire: **DesertTower > VFX > Build Obelisk Destruction + Wire**.

Render and validate: **DesertTower > VFX > Build Obelisk Destruction + Preview**. Writes 100 frames and `validation.txt` under `Docs/vfx-preview/ObeliskDestruction` without replacing the open scene.

PlayMode tests: `DesertTower.VFX.Tests.ObeliskDestructionTests` (actual prefab death, transform alignment, VFX surviving tower removal, pooled replay).
