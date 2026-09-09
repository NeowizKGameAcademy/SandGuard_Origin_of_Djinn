import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'EnemyEquipment.blend'))
root=bpy.data.objects['ChiefCape']; loc=root.location.copy(); rot=root.rotation_euler.copy()
root.location=(0,0,0); root.rotation_euler=(0,0,0)
arm=bpy.data.objects['ChiefCape_Rig']; arm.data.pose_position='POSE'
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.collections['ChiefCape'].objects: o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Exports/ChiefCape.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,path_mode='COPY',embed_textures=False)
root.location=loc; root.rotation_euler=rot
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'EnemyEquipment.blend'))
