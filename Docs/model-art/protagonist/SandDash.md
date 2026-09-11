# Sand Dash

`SandDash.blend` is the editable Blender source; `character-protagonist-sand-dash.fbx` is the exported Humanoid animation. Rebuild with Blender 5.2: `blender -b --python build_sand_dash.py` from this folder (the script resolves source paths relative to itself).

The existing `Standing Run Forward` frame 5 supplies the rig and starting pose. The authored six keys (frames 1, 3, 5, 8, 10, 12 at 30fps) add a lowered forward lean, a bent leading knee, an extended trailing leg, and opposing arm reaches. Hip travel is removed; PlayerMotor still owns actual displacement. Original Mixamo sources remain unchanged.

Candidate audit: `aerodash` folds both legs during an airborne hop, `Pushing` shuffles in a pushing stance, `Flying` is horizontal flight; none matches the requested long-stride dash directly. Running was used as an editable starting point.

Unity: `SandGuard > Player > Connect Authored Sand Dash` imports `MobilityAnimations/SandDash.fbx`. The historical `Dash Legs` layer name is retained, but its mask includes the whole Humanoid and the layer sits below casting and hit reactions. A normal dash uses the full pose; upper-body attacks can override it. No root-motion movement is applied.
