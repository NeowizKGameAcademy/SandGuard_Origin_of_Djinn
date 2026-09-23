import bpy,json
from pathlib import Path
R=Path(r'C:/course/unity/SandGuard');out=[]
for file in ['Assets/Player/Art/Protagonist/Protagonist.fbx','Assets/Player/Art/Protagonist/MobilityAnimations/CrouchPose.fbx','Assets/Player/Art/Protagonist/CombatAnimations/Hit.fbx']:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(R/file))
    a=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    out.append(dict(file=file,rotation=list(a.rotation_euler),scale=list(a.scale),actions=[dict(name=x.name,range=list(x.frame_range)) for x in bpy.data.actions],bone_points={b.name:list((a.matrix_world@b.matrix).translation) for b in a.pose.bones if b.name.split(':')[-1] in ['Hips','Head','LeftArm','LeftForeArm','LeftHand','RightHand','LeftFoot','RightFoot']}))
(R/'Docs/3.Ending/Video/production/previs49/motion-inspect.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
