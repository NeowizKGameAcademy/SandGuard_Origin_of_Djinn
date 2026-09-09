"""Export the existing GLB bodies without changing their downloaded originals."""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SPECS=[('Swordsman','bandit-minion'),('Assassin','bandit-ninja'),('ShieldGuard','bandit-shielder'),('HammerBrute','bandit-hammerer'),('Chief','bandit-leader')]
report=[]
for name,folder in SPECS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src=next((ROOT/'Docs/model-art'/folder).glob('*.glb'))
    out=ROOT/'Assets/Enemy/Art/Characters'/name; out.mkdir(parents=True,exist_ok=True)
    bpy.ops.import_scene.gltf(filepath=str(src))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for o in meshes:
        o.name=name+'_BodyMesh'
        for m in o.data.materials:
            m.name=name+'_Body'
            tex=next(n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
            tex.filepath_raw=str(out/(name+'_BaseColor.png')); tex.file_format='PNG'; tex.save()
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(out/(name+'_Body.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    report.append({'name':name,'source':str(src.relative_to(ROOT)),'meshes':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)})
(Path(__file__).parent/'preview_body_sources.json').write_text(json.dumps(report,indent=2))
print('PREVIEW_BODIES_EXPORTED',json.dumps(report))
