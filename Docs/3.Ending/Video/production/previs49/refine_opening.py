import bpy,math,sys,json
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s=bpy.context.scene;cam=bpy.data.objects['R01 camera'];rig=bpy.data.objects['Actor blocking root']
cam.animation_data_clear();cam.data.animation_data_clear()
def ease(x):x=max(0,min(1,x));return x*x*(3-2*x)
report=[]
for f in range(1,109):
    s.frame_set(f);u=(f-1)/107;k=ease(u);reveal=ease((u-.48)/.52);a=math.radians(48-100*k)
    cam.location=(math.cos(a)*4.65,math.sin(a)*4.65+.25,1.12+.68*k)
    target=Vector((-.3*reveal,rig.location.y+1.45*reveal,1.08+.9*reveal))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    focus=(rig.location+Vector((0,0,1))).lerp(Vector((-.65,5,2.1)),ease((reveal-.5)/.5))
    cam.data.lens=35;cam.data.dof.focus_distance=(focus-cam.location).length
    for p in ['location','rotation_euler']:cam.keyframe_insert(p,frame=f)
    cam.data.keyframe_insert('dof.focus_distance',frame=f)
    if f in [1,36,72,108]:report.append(dict(frame=f,camera=list(cam.location),target=list(target)))
s.frame_set(1);s.camera=cam;s.frame_start=1;s.frame_end=1176;s.render.filepath=str(out/'frames/f_')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
(out/'qa/opening-revision.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
if '--preview' in sys.argv:
    for f in [1,36,72,108]:
        s.frame_set(f);s.camera=cam;s.render.filepath=str(out/'qa'/f'opening-revised-{f:04d}.png');bpy.ops.render.render(write_still=True)
else:
    s.frame_end=108;bpy.ops.render.render(animation=True)
