import bpy,json,sys
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s=bpy.context.scene;arm=bpy.data.objects['Armature'];lamp=bpy.data.objects['LampMesh']
report=[]
for f in [289,337,360,552,907,923,933,936,937,945,960,1008]:
    s.frame_set(f)
    marker=max((m for m in s.timeline_markers if m.frame<=f),key=lambda m:m.frame);s.camera=marker.camera
    seal=lamp.matrix_world@Vector((.09,.10,-.087))
    tip=arm.matrix_world@arm.pose.bones['mixamorig:RightHandIndex3'].tail
    report.append(dict(frame=f,camera=s.camera.name,seal=list(seal),tip=list(tip),tipGap=(seal-tip).length))
    if '--render' in sys.argv:
        s.render.filepath=str(out/'qa'/f'check-{f:04d}.png');bpy.ops.render.render(write_still=True)
(out/'qa/contact-check.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
