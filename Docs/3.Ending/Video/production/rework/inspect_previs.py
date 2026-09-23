import bpy, json
from pathlib import Path
from mathutils import Vector
root=Path(r'C:/course/unity/SandGuard')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Player/Art/Protagonist/Walk.fbx'))
def info(o):
    return dict(name=o.name,type=o.type,location=list(o.location),dimensions=list(o.dimensions),bounds=[list(o.matrix_world@Vector(v)) for v in o.bound_box] if o.type=='MESH' else None,materials=[s.name for s in o.data.materials] if o.type=='MESH' else None,bones=[b.name for b in o.data.bones] if o.type=='ARMATURE' else None,action=o.animation_data.action.name if o.animation_data and o.animation_data.action else None)
result=dict(objects=[info(o) for o in bpy.context.scene.objects],actions=[dict(name=a.name,range=list(a.frame_range)) for a in bpy.data.actions])
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Player/Art/Lamp/Lamp.fbx'))
result['with_lamp']=[info(o) for o in bpy.context.scene.objects if o.type=='MESH']
(root/'Docs/3.Ending/Video/production/rework/inspect.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
