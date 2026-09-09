import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]; OUT=Path(__file__).parent/'new-rigs'; OUT.mkdir(exist_ok=True)
report=[]
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for folder,name in [('bandit-hammerer','HammerBrute'),('bandit-leader','Chief'),('bandit-shielder','ShieldGuard'),('bandit-ninja','Assassin')]:
    if selected and name not in selected: continue
    path=next((ROOT/folder).rglob('*@*.fbx'))
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=str(path))
    scene=bpy.context.scene; rig=next(o for o in scene.objects if o.type=='ARMATURE'); body=next(o for o in scene.objects if o.type=='MESH')
    missing=[n for n in ['Hips','Spine','Head','LeftArm','RightArm','LeftForeArm','RightForeArm','LeftHand','RightHand','LeftUpLeg','RightUpLeg','LeftLeg','RightLeg','LeftFoot','RightFoot'] if not any(b.name.endswith(':'+n) or b.name==n for b in rig.data.bones)]
    weights=[sum(g.weight for g in v.groups) for v in body.data.vertices]
    action=rig.animation_data.action; start,end=action.frame_range
    rig.data.pose_position='REST'; bpy.context.view_layer.update()
    dg=bpy.context.evaluated_depsgraph_get(); ev=body.evaluated_get(dg); temp=ev.to_mesh(); bind=[body.matrix_world@v.co for v in temp.vertices]; ev.to_mesh_clear()
    lo=Vector([min(p[i] for p in bind) for i in range(3)]); hi=Vector([max(p[i] for p in bind) for i in range(3)]); h=hi.z-lo.z; factor=1.8/h
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    rig.data.pose_position='POSE'
    mat=bpy.data.materials.new(name+'_Review'); mat.use_nodes=True
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(ROOT.parents[1]/'Assets/Enemy/Art/Characters'/name/(name+'_BaseColor.png')))
    mat.node_tree.links.new(tex.outputs['Color'],mat.node_tree.nodes['Principled BSDF'].inputs['Base Color']); mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8
    snapshots=[]; local_motion=[]; sampled=[]
    for col,fraction in enumerate([.15,.45,.75]):
        frame=round(start+(end-start)*fraction); scene.frame_set(frame); bpy.context.view_layer.update()
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get()); mesh=bpy.data.meshes.new_from_object(ev)
        pts=[body.matrix_world@v.co for v in mesh.vertices]
        assert all(math.isfinite(c) for p in pts for c in p)
        for v,p in zip(mesh.vertices,pts): v.co=(p-center)*factor+Vector(((col-1)*2.3,0,0))
        obj=bpy.data.objects.new('Pose_'+str(frame),mesh); scene.collection.objects.link(obj); mesh.materials.clear(); mesh.materials.append(mat); snapshots.append(obj)
        local_motion.append([(p-center)*factor for p in pts]); sampled.append(frame)
    for o in [body,rig]: o.hide_render=True
    movement=max((a-b).length for a,b in zip(local_motion[0],local_motion[1]))
    assert not missing and min(weights)>.00001 and movement>.01
    report.append({'character':name,'file':str(path.relative_to(ROOT)),'bones':len(rig.data.bones),'bone_names':[b.name for b in rig.data.bones],'missing_major_bones':missing,'unweighted_vertices':sum(w<.00001 for w in weights),'max_weight_sum_error':max(abs(w-1) for w in weights),'duration_seconds':float((end-start)/scene.render.fps),'sample_frames':sampled,'sample_vertex_motion_at_1_8m_scale':movement})
    world=bpy.data.worlds.new('World'); scene.world=world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.1,.13,.19,1); world.node_tree.nodes['Background'].inputs[1].default_value=.55
    for loc,power in [((-3,-5,6),850),((4,-2,4),500)]:
        data=bpy.data.lights.new('Softbox','AREA'); data.energy=power; data.size=5
        obj=bpy.data.objects.new('Softbox',data); scene.collection.objects.link(obj); obj.location=loc; obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
    camdata=bpy.data.cameras.new('Camera'); cam=bpy.data.objects.new('Camera',camdata); scene.collection.objects.link(cam); scene.camera=cam
    pts=[v.co for o in snapshots for v in o.data.vertices]; lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)]); target=(lo+hi)/2
    cam.location=target+Vector((0,-12,2)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); camdata.type='ORTHO'; camdata.ortho_scale=max(hi.x-lo.x+1,(hi.z-lo.z+1)*2.4)
    scene.render.engine='CYCLES'; scene.cycles.samples=12; scene.render.resolution_x=1440; scene.render.resolution_y=600; scene.render.resolution_percentage=100; scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(OUT/(name+'_poses.png'))
    bpy.ops.render.render(write_still=True)
(OUT/('report_'+ '_'.join(selected)+'.json' if selected else 'report.json')).write_text(json.dumps(report,indent=2))
print('NEW_RIGS_CHECKED',json.dumps([{k:v for k,v in r.items() if k!='bone_names'} for r in report]))
