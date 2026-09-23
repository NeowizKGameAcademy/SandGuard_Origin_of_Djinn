import bpy
from pathlib import Path
from mathutils import Vector,Matrix
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s=bpy.context.scene;cam=bpy.data.objects['R09 camera'];arm=bpy.data.objects['Armature'];rig=bpy.data.objects['Actor blocking root']
for f in range(553,673):
    s.frame_set(f);bpy.context.view_layer.update()
    head=(arm.matrix_world@arm.pose.bones['mixamorig:Head'].matrix).translation+Vector((0,0,.17))
    target=head+Matrix.Rotation(rig.rotation_euler.z,3,'Z')@Vector((-.025,0,-.13))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.keyframe_insert('rotation_euler',frame=f)
s.frame_set(1);s.camera=bpy.data.objects['R01 camera'];s.render.filepath=str(out/'frames/f_')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s.frame_start=553;s.frame_end=672;s.camera=cam;bpy.ops.render.render(animation=True)
