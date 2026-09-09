import bpy,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
paths=[]
for folder in ROOT.glob('bandit-*'): paths+=list(folder.rglob('*.fbx'))
for folder in ROOT.glob('*Pack'):
    f=list(folder.glob('*.fbx'))
    paths+=([folder/'bandit1.fbx'] if (folder/'bandit1.fbx').exists() else [])
    sample=next((p for p in f if p.name!='bandit1.fbx'),None)
    if sample: paths.append(sample)
paths+=list(ROOT.glob('*.fbx'))[:2]
report=[]; cache={}
for path in paths:
    digest=hashlib.sha256(path.read_bytes()).hexdigest()
    if digest in cache:
        row=dict(cache[digest]); row['file']=str(path.relative_to(ROOT)); row['identical_to']=cache[digest]['file']; report.append(row); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    row={'file':str(path.relative_to(ROOT)),'meshes':len(meshes),'bones':sum(len(o.data.bones) for o in rigs),'skinned_meshes':sum(any(m.type=='ARMATURE' for m in o.modifiers) and len(o.vertex_groups)>0 for o in meshes),'actions':len(bpy.data.actions),'wrist_bones':[b.name for o in rigs for b in o.data.bones if 'Hand' in b.name and not any(t in b.name for t in ['Thumb','Index','Middle','Ring','Pinky'])],'finger_bones':sum(any(s in b.name for s in ['Thumb','Index','Middle','Ring','Pinky']) for o in rigs for b in o.data.bones)}
    report.append(row); cache[digest]=row
(Path(__file__).parent/'mixamo_rig_audit.json').write_text(json.dumps(report,indent=2))
print('RIG_AUDIT',json.dumps(report))
