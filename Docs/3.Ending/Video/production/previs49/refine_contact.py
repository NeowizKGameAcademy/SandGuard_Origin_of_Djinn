import bpy,json
from pathlib import Path
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s=bpy.context.scene
def ease(x):x=max(0,min(1,x));return x*x*(3-2*x)
node=bpy.data.materials['Right fingertips price'].node_tree.nodes['Hand price glow'].outputs[0]
cyan=bpy.data.materials['Blue magic'].node_tree.nodes['Principled BSDF'].inputs['Emission Strength']
bits=[ob for ob in s.objects if ob.name.startswith('Fingertip light ')]
changed=[]
for f in list(range(289,457))+list(range(673,733)):
    s.frame_set(f);sec=(f-1)/24
    press=ease((sec-12.6)/1.0) if sec<15 else 1 if sec<16.5 else 1-ease((sec-16.5)/.75) if sec<19 else ease((sec-28)/.55)
    glow=ease((press-.90)/.10)*.82;old=press*.82
    node.default_value=glow;node.keyframe_insert('default_value',frame=f)
    cyan.default_value=.4+1.2*glow;cyan.keyframe_insert('default_value',frame=f)
    for j,ob in enumerate(bits):
        index=int(ob.name.rsplit(' ',1)[1]);t=(sec*1.7+index/12)%1
        import math
        r=.010*glow*math.sin(math.pi*t);ob.scale=(r,r,r);ob.keyframe_insert('scale',frame=f)
    if abs(old-glow)>.00001:changed.append(f)
s.frame_set(1);s.camera=bpy.data.objects['R01 camera'];s.render.filepath=str(out/'frames/f_');s.frame_end=1176
bpy.ops.wm.save_as_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
(out/'qa/contact-revision.json').write_text(json.dumps(dict(reason='Fingertip cost starts at contact, not while reaching.',frames=changed),indent=2),encoding='utf-8')
for f in changed:
    s.frame_set(f);s.camera=max((m for m in s.timeline_markers if m.frame<=f),key=lambda m:m.frame).camera
    s.render.filepath=str(out/'frames'/f'f_{f:04d}.png');bpy.ops.render.render(write_still=True)
