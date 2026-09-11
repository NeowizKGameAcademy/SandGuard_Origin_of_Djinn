"""Author a non-looping in-place dash from the existing Mixamo running rig. Blender 5.2."""
import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix

root=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'character-protagonist-mixamo@Standing Run Forward.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
scene=bpy.context.scene
scene.frame_set(5)
bpy.context.view_layer.update()
bones={b.name.split(':')[-1]:b for b in rig.pose.bones}
base={n:b.matrix_basis.copy() for n,b in bones.items()}
world=rig.matrix_world.copy(); inv=world.inverted()
def point(n): return world@bones[n].head
def move_root(target):
    m=bones['Hips'].matrix.copy(); m.translation=inv@target; bones['Hips'].matrix=m; bpy.context.view_layer.update()
def turn(n,angle):
    b=bones[n]; m=b.matrix.copy(); p=m.translation.copy()
    # Convert rotation in Blender world space to armature coordinates.
    rot=world.to_quaternion().inverted().to_matrix()@Matrix.Rotation(angle,3,'X')@world.to_quaternion().to_matrix()
    m=rot.to_4x4()@m; m.translation=p; b.matrix=m; bpy.context.view_layer.update()
def aim(n,target):
    b=bones[n]; direction=(inv@target)-b.head
    q=(b.tail-b.head).rotation_difference(direction)
    m=q.to_matrix().to_4x4()@b.matrix; m.translation=b.head.copy(); b.matrix=m; bpy.context.view_layer.update()
def limb(upper,lower,end,target,pole):
    a=point(upper); b=point(lower); c=point(end)
    l1=(b-a).length; l2=(c-b).length
    delta=target-a; d=min(delta.length,(l1+l2)*.992); axis=delta.normalized()
    side=pole-a; side=(side-axis*side.dot(axis)).normalized()
    along=(l1*l1-l2*l2+d*d)/(2*d)
    knee=a+axis*along+side*math.sqrt(max(0,l1*l1-along*along))
    aim(upper,knee); aim(lower,a+axis*d)

rig.animation_data_clear()
for frame,weight in [(1,.35),(3,.9),(5,1),(8,1),(10,.9),(12,.45)]:
    scene.frame_set(frame)
    for n,b in bones.items(): b.matrix_basis=base[n].copy()
    bpy.context.view_layer.update()
    hip=point('Hips'); hip.y=0; hip.z-=.075*weight
    move_root(hip)
    turn('Hips',math.radians(8)*weight)
    turn('Spine',math.radians(20)*weight)
    turn('Spine1',math.radians(6)*weight)
    turn('Head',math.radians(-18)*weight)
    # Front knee stays bent, rear leg extends back toward the launch point.
    for side,offset in [('Left',Vector((.055,-.26,.15))),('Right',Vector((-.055,.36,.13)))]:
        target=Vector((hip.x+offset.x,offset.y,offset.z))
        target=point(side+'Foot').lerp(target,weight)
        foot=bones[side+'Foot'].matrix.copy()
        limb(side+'UpLeg',side+'Leg',side+'Foot',target,hip+Vector((offset.x,-.6,-.1)))
        foot.translation=bones[side+'Foot'].head.copy(); bones[side+'Foot'].matrix=foot
        bpy.context.view_layer.update()
    # Opposed reach: right hand forward, left hand swept back.
    for side,offset in [('Right',Vector((-.19,-.41,.14))),('Left',Vector((.20,.22,.08)))]:
        hand=point(side+'Hand').lerp(hip+offset,weight)
        pole=hip+Vector((offset.x*2,offset.y*.3,-.1))
        limb(side+'Arm',side+'ForeArm',side+'Hand',hand,pole)
    for b in rig.pose.bones:
        b.rotation_mode='QUATERNION'
        b.keyframe_insert('location',frame=frame)
        b.keyframe_insert('rotation_quaternion',frame=frame)
        b.keyframe_insert('scale',frame=frame)
rig.animation_data.action.name='Sand Dash'
scene.frame_start=1; scene.frame_end=12; scene.render.fps=30
scene.frame_set(5)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'SandDash.blend'))
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(root/'character-protagonist-sand-dash.fbx'),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
print('SAND_DASH_AUTHORED',flush=True)
