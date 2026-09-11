import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
result={}
for folder in (ROOT/'Docs/model-art').glob('bandit-*'):
    files=list(folder.glob('*.glb'))
    if not files: continue
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=str(files[0]))
    pts=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices]
    hands={}
    for side in [-1,1]:
        p=[v for v in pts if side*v.x>.22 and .375<v.z<.455]
        hands[str(side)]={'center':[sum(v[i] for v in p)/len(p) for i in range(3)],'min':[min(v[i] for v in p) for i in range(3)],'max':[max(v[i] for v in p) for i in range(3)]}
    result[folder.name]=hands
(Path(__file__).parent/'hand_regions.json').write_text(json.dumps(result,indent=2))
