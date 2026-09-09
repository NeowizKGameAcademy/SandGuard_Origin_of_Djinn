"""Read source FBXs; measure skeleton motion without modifying source assets."""
import bpy, json, hashlib, math
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'animation-selection-v1'
OUT.mkdir(exist_ok=True)
paths=sorted([p for d in ROOT.glob('*Pack') for p in d.glob('*.fbx') if p.name!='bandit1.fbx']+list(ROOT.glob('*.fbx')))
rows=[]
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    action=rig.animation_data.action
    start,end=map(float,action.frame_range)
    fps=bpy.context.scene.render.fps/bpy.context.scene.render.fps_base
    short=lambda name:name.split(':')[-1]
    bones={short(b.name):b for b in rig.pose.bones}
    samples=[]; hips=[]; hands=[]; signature=[]
    for i in range(31):
        frame=start+(end-start)*i/30
        bpy.context.scene.frame_set(math.floor(frame),subframe=frame%1)
        bpy.context.view_layer.update()
        points={name:rig.matrix_world@b.head for name,b in bones.items()}
        hips.append(points['Hips'].copy())
        hands.append([points['LeftHand'].copy(),points['RightHand'].copy()])
        signature.extend(round(c,5) for name in sorted(points) for c in points[name])
        if i in [0,6,12,18,24,30]:
            samples.append({'time':round((frame-start)/fps,4),'bones':{name:[list(points[name]),list(rig.matrix_world@b.tail)] for name,b in bones.items() if not any(s in name for s in ['Thumb','Index','Middle','Ring','Pinky','End'])}})
    delta=hips[-1]-hips[0]
    row={'source':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'duration_seconds':round((end-start)/fps,4),'fps':fps,'frames':[start,end],'bones':len(bones),'meshes':sum(o.type=='MESH' for o in bpy.context.scene.objects),'hips_delta_blender_xyz':list(delta),'hips_horizontal_span':math.hypot(max(p.x for p in hips)-min(p.x for p in hips),max(p.y for p in hips)-min(p.y for p in hips)),'hips_start_end_z':[hips[0].z,hips[-1].z],'hand_distance_range':[min((a-b).length for a,b in hands),max((a-b).length for a,b in hands)],'sampled_motion_sha256':hashlib.sha256(json.dumps(signature).encode()).hexdigest(),'samples':samples}
    rows.append(row)
    print('AUDITED',row['source'],flush=True)
(OUT/'library-audit.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('DONE',len(rows),flush=True)
