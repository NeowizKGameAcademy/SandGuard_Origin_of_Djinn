import bpy, json
from pathlib import Path
root=Path('C:/course/unity/SandGuard')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/DesertTemple/Editor/DesertTemple_Visual.fbx'))
report=[]
for o in bpy.context.scene.objects:
    if o.type!='MESH': continue
    report.append(dict(name=o.name, vertices=len(o.data.vertices), faces=len(o.data.polygons), dimensions=list(o.dimensions), location=list(o.location), materials=[m.name if m else None for m in o.data.materials], uv=[u.name for u in o.data.uv_layers]))
print('TEMPLE_INSPECT',len(report),'meshes;',sum(not o['uv'] for o in report),'without UV')
(root/'Docs/LevelArt/TrimSheet/source-inspection.json').write_text(json.dumps(report,indent=2))
for m in bpy.data.materials:
    print('MATERIAL',m.name,[(n.type, n.image.filepath if n.type=='TEX_IMAGE' and n.image else '') for n in m.node_tree.nodes] if m.use_nodes else list(m.diffuse_color))
