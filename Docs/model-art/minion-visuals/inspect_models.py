import bpy, os, json
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
report={}
for kind in ['skeleton','anubis']:
 folder=os.path.join(ROOT,'Docs/model-art',kind+'-minion')
 base=next(x for x in os.listdir(folder) if x.endswith('.fbx') and '@' not in x)
 idle=next(x for x in os.listdir(folder) if x.endswith('@Sword And Shield Idle.fbx' if kind=='skeleton' else '@Standing Idle.fbx'))
 for label,file in [('base',base),('rigged',idle)]:
  bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
  bpy.ops.import_scene.fbx(filepath=os.path.join(folder,file))
  meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
  points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
  lo=Vector(tuple(min(v[i] for v in points) for i in range(3))); hi=Vector(tuple(max(v[i] for v in points) for i in range(3)))
  report[kind+'_'+label]={'file':file,'bounds_min':list(lo),'bounds_max':list(hi),'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'materials':[m.name for m in o.data.materials if m]} for o in meshes],'armatures':[{'name':o.name,'bones':len(o.data.bones)} for o in bpy.context.scene.objects if o.type=='ARMATURE'],'images':[i.filepath for i in bpy.data.images if i.source=='FILE']}
  if label=='rigged':
   for a in [o for o in bpy.context.scene.objects if o.type=='ARMATURE']: a.data.pose_position='REST'
   bpy.context.view_layer.update()
   points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
   lo=Vector(tuple(min(v[i] for v in points) for i in range(3))); hi=Vector(tuple(max(v[i] for v in points) for i in range(3)))
   scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=16; scene.world.color=(.15,.15,.15)
   center=(lo+hi)/2; height=hi.z-lo.z
   def aim(o): o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
   for pos,power in [((-2,-3,4),400),((3,-1,2),250),((0,3,3),450)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(pos)*height); light=bpy.context.object; light.data.energy=power*height*height; light.data.size=height*2; aim(light)
   bpy.ops.object.camera_add(location=center+Vector((.25,-3,.1))*height); camera=bpy.context.object; aim(camera); camera.data.type='ORTHO'; camera.data.ortho_scale=max(height,hi.x-lo.x)*1.25; scene.camera=camera
   scene.render.resolution_x=900; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
   scene.render.filepath=os.path.join(os.path.dirname(__file__),kind+'-source.png'); bpy.ops.render.render(write_still=True)
with open(os.path.join(os.path.dirname(__file__),'source-report.json'),'w') as f: json.dump(report,f,indent=2)
