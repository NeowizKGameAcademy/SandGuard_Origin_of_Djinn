import bpy, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
report={}
for folder in sorted(root.glob('bandit-*')):
    paths=list(folder.glob('*.glb'))
    if not paths: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(paths[0]))
    bpy.context.view_layer.update()
    pts=[o.matrix_world@Vector(p) for o in bpy.data.objects if o.type=='MESH' for p in o.bound_box]
    lo=[min(p[i] for p in pts) for i in range(3)]
    hi=[max(p[i] for p in pts) for i in range(3)]
    report[folder.name]={'bounds': [lo,hi], 'size': [hi[i]-lo[i] for i in range(3)],'armatures': [o.name for o in bpy.data.objects if o.type=='ARMATURE']}
(Path(__file__).parent/'body_dimensions.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
