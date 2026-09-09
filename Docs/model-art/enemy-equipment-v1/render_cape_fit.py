import bpy,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'EnemyEquipment.blend'))
scene=bpy.context.scene
for o in list(scene.objects):
    if o.type not in {'LIGHT','CAMERA'}: o.hide_render=True
for o in bpy.data.collections['ChiefCape'].objects: o.hide_render=False
cape=bpy.data.objects['ChiefCape']; cape.location=(0,.10,1.45); cape.rotation_euler=(0,0,0)
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=str(ROOT.parent/'bandit-leader/fantasy+warrior+3d+model.glb'))
added=set(bpy.data.objects)-before
for o in added:
    o.hide_render=False
    if o.parent is None: o.scale*=1.8
cam=scene.camera; cam.data.ortho_scale=2.2
scene.render.resolution_x=900; scene.render.resolution_y=1000; scene.cycles.samples=16
scene.frame_set(13)
for name,pos in [('ChiefCape_Fit_Front',(3,-6,2.9)),('ChiefCape_Fit_Rear',(-3,6,2.9))]:
    cam.location=pos; cam.rotation_euler=(Vector((0,0,.93))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(ROOT/'Previews'/f'{name}.png'); bpy.ops.render.render(write_still=True)
