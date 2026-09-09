import bpy, json, os, hashlib
from pathlib import Path
root = Path(__file__).resolve().parent
report = []
for path in sorted(root.glob('*')):
    if path.suffix.lower() not in ('.fbx', '.glb'): continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix.lower() == '.fbx': bpy.ops.import_scene.fbx(filepath=str(path))
    else: bpy.ops.import_scene.gltf(filepath=str(path))
    entry = {'file':path.name, 'objects':[]}
    for obj in bpy.context.scene.objects:
        item = {'name':obj.name,'type':obj.type,'dimensions':list(obj.dimensions)}
        if obj.type == 'ARMATURE': item['bones'] = len(obj.data.bones)
        if obj.type == 'MESH':
            uv = obj.data.uv_layers.active
            uv_values = sorted(tuple(round(float(v),5) for v in x.uv) for x in uv.data) if uv else []
            item.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons), uv=len(uv_values), uv_hash=hashlib.sha256(json.dumps(uv_values).encode()).hexdigest(), materials=[m.name for m in obj.data.materials if m], groups=len(obj.vertex_groups))
        entry['objects'].append(item)
    report.append(entry)
(root/'model-inspection.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
