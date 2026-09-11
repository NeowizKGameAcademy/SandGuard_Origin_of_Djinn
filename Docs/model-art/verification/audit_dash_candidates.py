import bpy,json,math
from pathlib import Path
root=Path('C:/course/unity/SandGuard')
rows=[]
for name in ['aerodash','Pushing','Running','Standing Run Forward','Flying']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/'Docs/model-art/protagonist'/('character-protagonist-mixamo@'+name+'.fbx')))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    action=rig.animation_data.action
    a,b=action.frame_range
    bones={p.name.split(':')[-1]:p for p in rig.pose.bones}
    row={'name':name,'frames':[a,b],'samples':[]}
    for f in [a+(b-a)*i/6 for i in range(7)]:
        bpy.context.scene.frame_set(int(f),subframe=f%1)
        row['samples'].append({'frame':f,'joints':{n:list(rig.matrix_world@bones[n].head) for n in ['Hips','Head','LeftUpLeg','LeftLeg','LeftFoot','RightUpLeg','RightLeg','RightFoot','LeftArm','LeftForeArm','LeftHand','RightArm','RightForeArm','RightHand']}})
    rows.append(row)
(root/'Logs/dash-candidates.json').write_text(json.dumps(rows,indent=2))
