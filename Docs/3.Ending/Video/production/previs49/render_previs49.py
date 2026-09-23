"""Package the editable scene with temporary audio and render shot-marker cameras."""
import bpy,sys
from pathlib import Path
out=Path(__file__).resolve().parent;root=out.parents[4]
bpy.ops.wm.open_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s=bpy.context.scene
for ob in s.objects:
    if ob.name.startswith('Magic current '):
        for f,hidden in [(1,True),(145,False),(937,True)]:
            ob.hide_render=hidden;ob.keyframe_insert('hide_render',frame=f)
seq=s.sequence_editor_create()
strips=seq.strips if hasattr(seq,'strips') else seq.sequences
if 'Temporary soundtrack' not in strips:
    strips.new_sound('Temporary soundtrack',str(out/'temp-mix.wav'),channel=1,frame_start=1)
s.sync_mode='AUDIO_SYNC';s.render.use_sequencer=False
if 'Native ending title' not in bpy.data.objects:
    font=bpy.data.curves.new('Ending title text','FONT');font.body='첫 번째 지니';font.align_x='CENTER';font.align_y='CENTER';font.size=.15
    font.font=bpy.data.fonts.load(str(root/'Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'))
    ob=bpy.data.objects.new('Native ending title',font);s.collection.objects.link(ob);ob.location=(0,0,-9.75)
    mat=bpy.data.materials.new('Ending title white');mat.use_nodes=True;p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(1,1,1,1);p.inputs['Emission Color'].default_value=(1,1,1,1);p.inputs['Emission Strength'].default_value=1
    ob.data.materials.append(mat)
for f,hidden in [(1,True),(1129,False)]:
    ob=bpy.data.objects['Native ending title'];ob.hide_render=hidden;ob.keyframe_insert('hide_render',frame=f)
s.frame_set(1);s.camera=bpy.data.objects['R01 camera'];s.render.filepath=str(out/'frames/f_')
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
if '--check' in sys.argv:
    for f in [337,552,907,923,936]:
        s.frame_set(f);s.camera=max((m for m in s.timeline_markers if m.frame<=f),key=lambda m:m.frame).camera
        s.render.filepath=str(out/'qa'/f'final-check-{f:04d}.png');bpy.ops.render.render(write_still=True)
else:
    (out/'frames').mkdir(exist_ok=True);s.frame_set(1);s.camera=bpy.data.objects['R01 camera'];bpy.ops.render.render(animation=True)
