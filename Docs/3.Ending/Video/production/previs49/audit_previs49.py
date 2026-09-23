import bpy,json
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(out/'SandGuard-Ending49-Previs.blend'))
s=bpy.context.scene;arm=bpy.data.objects['Armature'];lamp=bpy.data.objects['LampMesh']
shots=json.loads((out/'shot-plan.json').read_text(encoding='utf-8'));checks=[]
for shot in shots:
    for f in [shot['startFrame'],(shot['startFrame']+shot['endFrame'])//2,shot['endFrame']]:
        s.frame_set(f);bpy.context.view_layer.update()
        camera=max((m for m in s.timeline_markers if m.frame<=f),key=lambda m:m.frame).camera
        assert camera.name==shot['id']+' camera'
        head=(arm.matrix_world@arm.pose.bones['mixamorig:Head'].matrix).translation+Vector((0,0,.17))
        seal=lamp.matrix_world@Vector((.09,.10,-.087))
        checks.append(dict(frame=f,shot=shot['id'],camera=list(camera.location),focalLength=camera.data.lens,focusDistance=camera.data.dof.focus_distance,lamp=list(lamp.location),seal=list(seal),head=list(head)))
contacts=[]
for f in [337,361,697,721,841,907]:
    s.frame_set(f);bpy.context.view_layer.update();seal=lamp.matrix_world@Vector((.09,.10,-.087));tip=arm.matrix_world@arm.pose.bones['mixamorig:RightHandIndex3'].tail
    contacts.append(dict(frame=f,tipGapMetres=(seal-tip).length))
s.frame_set(985);bpy.context.view_layer.update();bottom=min((lamp.matrix_world@Vector(v)).z for v in lamp.bound_box)
s.frame_set(108);cam=bpy.data.objects['R01 camera'];p=world_to_camera_view(s,cam,Vector((-.65,5,2.1)))
report=dict(frameStart=s.frame_start,frameEnd=s.frame_end,fps=s.render.fps,cameraMarkers=len(s.timeline_markers),cameraChecks=len(checks),contactChecks=contacts,lampSettledBottomMetres=bottom,openingCrystalInFrame=bool(0<p.x<1 and 0<p.y<1 and p.z>0),packedSounds=[dict(name=v.name,packed=bool(v.packed_file)) for v in bpy.data.sounds],fontPacked=bool(bpy.data.fonts.get('Pretendard Light') and bpy.data.fonts['Pretendard Light'].packed_file))
(out/'blocking-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
(out/'native-scene-check.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
