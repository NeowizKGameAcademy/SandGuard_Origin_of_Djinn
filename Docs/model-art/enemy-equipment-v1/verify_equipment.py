import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
manifest=json.loads((ROOT/'equipment_manifest.json').read_text())
report=[]
for info in manifest['assets']:
    for ext in ['fbx','glb']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path=ROOT/'Exports'/f'{info["name"]}.{ext}'
        if ext=='fbx': bpy.ops.import_scene.fbx(filepath=str(path))
        else: bpy.ops.import_scene.gltf(filepath=str(path))
        scene=bpy.context.scene; scene.frame_set(1); bpy.context.view_layer.update()
        rigs=[o for o in scene.objects if o.type=='ARMATURE']
        bone_shapes={b.custom_shape for rig in rigs for b in rig.pose.bones if b.custom_shape}
        meshes=[o for o in scene.objects if o.type=='MESH' and o not in bone_shapes]
        assert len(meshes)==1, (path,len(meshes))
        o=meshes[0]; tri=sum(len(p.vertices)-2 for p in o.data.polygons)
        assert tri==info['triangles'],(path,tri,info['triangles'])
        assert o.data.uv_layers and len(o.data.materials)==1,path
        images=[n.image for mat in o.data.materials for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        assert images and all(im.size[0]>0 for im in images),path
        assert len(rigs)==(1 if info['bones'] else 0),path
        points=[o.matrix_world@v.co for v in o.data.vertices]
        dims=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
        if not rigs:
            error=max(abs(a-b) for a,b in zip(dims,info['dimensions_xyz_m']))
            assert error<.002,(path,error,dims)
        record={'file':path.name,'triangles':tri,'texture_loaded':True,'dimensions_xyz_m':dims}
        if rigs:
            rig=rigs[0]; assert len(rig.data.bones)==13,path
            assert rig.animation_data and rig.animation_data.action,path
            weight_error=max(abs(sum(g.weight for g in v.groups)-1) for v in o.data.vertices)
            assert weight_error<.0001,(path,weight_error)
            def evaluated(frame):
                scene.frame_set(frame); bpy.context.view_layer.update()
                ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get()); mesh=ev.to_mesh()
                pts=[ev.matrix_world@v.co for v in mesh.vertices]; ev.to_mesh_clear(); return pts
            start,end=rig.animation_data.action.frame_range
            a=evaluated(int(start)); b=evaluated(int(start+(end-start)/4)); c=evaluated(int(end))
            motion=max((p-q).length for p,q in zip(a,b)); loop_error=max((p-q).length for p,q in zip(a,c))
            record.update(bones=13,weight_sum_error=weight_error,motion_m=motion,loop_error_m=loop_error,frame_range=[start,end])
            assert motion>.005,(path,'no motion',motion)
            assert loop_error<.003,(path,'loop mismatch',loop_error)
        report.append(record)
(ROOT/'verification.json').write_text(json.dumps(report,indent=2))
print('VERIFIED',json.dumps(report))
