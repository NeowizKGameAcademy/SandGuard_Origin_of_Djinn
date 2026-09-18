import bpy, math, os, json, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
OUT=os.path.join(ROOT,'Assets/GoldenArsenal/Models')
DOC=os.path.dirname(__file__)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
random.seed(17)
def mat(name,c,metal,rough,emit=0):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
 p.inputs['Emission Color'].default_value=(*c,1); p.inputs['Emission Strength'].default_value=emit
 return m
gold=mat('GA_Gold',(0.95,.48,.025),.72,.28)
edge=mat('GA_GoldLight',(1,.72,.09),.65,.25)
darkgold=mat('GA_GoldShadow',(.38,.15,.012),.65,.32)
steel=mat('GA_Silver',(.72,.73,.69),.7,.3)
dark=mat('GA_Obsidian',(.045,.044,.05),.25,.5)
leather=mat('GA_Grip',(.07,.045,.027),0,.8)
cyan=mat('GA_Turquoise',(.008,.62,.72),.4,.18,.22)
light=mat('GA_GemLight',(.025,.9,.96),.3,.16,.3)
materials=[gold,edge,darkgold,steel,dark,leather,cyan,light]
parts=[]; models={}
def mesh(name,v,f,mats,indices=None):
 me=bpy.data.meshes.new(name); me.from_pydata(v,[],f); me.update(); o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o)
 for m in mats: me.materials.append(m)
 if indices:
  for p,i in zip(me.polygons,indices): p.material_index=i
 parts.append(o); return o
def slab(name,poly,depth,material,y=0,bevel=0):
 n=len(poly); v=[(x,y+d,z) for d in [-depth/2,depth/2] for x,z in poly]
 f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 o=mesh(name,v,f,[material])
 if bevel:
  bpy.context.view_layer.objects.active=o; o.select_set(True); mod=o.modifiers.new('Cut bevel','BEVEL'); mod.width=bevel; mod.segments=1; bpy.ops.object.modifier_apply(modifier=mod.name); o.select_set(False)
 return o
def box(name,x,z,w,h,d,m,y=0,b=.01): return slab(name,[(x-w/2,z-h/2),(x+w/2,z-h/2),(x+w/2,z+h/2),(x-w/2,z+h/2)],d,m,y,b)
def ring(name,outer,inner,y,depth):
 n=len(outer); v=[(x,yy,z) for pts,yy in [(outer,y+depth),(outer,y),(inner,y),(inner,y+depth)] for x,z in pts]; f=[]; ids=[]
 for a,b,idx in [(0,n,2),(n,2*n,0),(2*n,3*n,2),(3*n,0,2)]:
  for i in range(n): j=(i+1)%n; f.append((a+i,a+j,b+j,b+i)); ids.append(idx if idx else (1 if i%3==0 else 0))
 return mesh(name,v,f,[gold,edge,darkgold],ids)
def gem(name,x,z,w,h,y):
 v=[(x,y-.045,z),(x-w/2,y,z),(x,y,z+h/2),(x+w/2,y,z),(x,y,z-h/2),(x,y+.03,z)]
 if abs(w-h)<.0001:
  v[1:5]=[(x-w/2,y,z-h/2),(x-w/2,y,z+h/2),(x+w/2,y,z+h/2),(x+w/2,y,z-h/2)]
 return mesh(name,v,[(0,1,2),(0,2,3),(0,3,4),(0,4,1),(5,2,1),(5,3,2),(5,4,3),(5,1,4)],[cyan,light],[0,1,0,0,0,0,0,0])
def cyl(name,z,r,length,m,vertices=8,y=0):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=length,location=(0,y,z)); o=bpy.context.object; o.name=name; o.data.materials.append(m); parts.append(o)
 bevel=o.modifiers.new('End facets','BEVEL'); bevel.width=min(.012,length*.18); bevel.segments=1; bpy.ops.object.modifier_apply(modifier=bevel.name); o.select_set(False); return o
def finish(name,pivot):
 global parts
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts: o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); o=bpy.context.object; o.name=name
 bpy.context.scene.cursor.location=(0,0,pivot); bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); o.location=(0,0,0)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT')
 o.data.calc_loop_triangles(); models[name]={'triangles':len(o.data.loop_triangles),'dimensions_m':list(o.dimensions)}
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=False)
 parts=[]; return o
# Sword: broad angular shoulders, gold cutting edges and a raised silver ridge.
outer=[(-.14,.27),(.14,.27),(.155,.78),(.19,.85),(.19,1.015),(0,1.29),(-.19,1.015),(-.19,.85),(-.155,.78)]
inner=[(-.103,.28),(.103,.28),(.105,.79),(.147,.85),(.147,.995),(0,1.245),(-.147,.995),(-.147,.85),(-.105,.79)]
slab('Blade gold core',outer,.045,gold,bevel=.003)
for sign in [-1,1]:
 v=[(x,sign*.026,z) for x,z in inner]+[(0,sign*.065,.28),(0,sign*.065,.82),(0,sign*.055,1.00),(0,sign*.026,1.245)]
 mesh('Silver ridge',v,[(0,1,9),(1,2,10,9),(2,3,4,11,10),(4,5,12,11),(5,6,11,12),(6,7,8,10,11),(8,0,9,10)],[steel])
box('Crossguard',0,.24,.49,.155,.12,gold,b=.028)
box('Gem setting',0,.21,.183,.18,.145,edge,b=.016)
box('Gem recess',0,.21,.117,.117,.01,darkgold,y=-.079,b=.003)
gem('Square blue gem',0,.21,.115,.115,-.09)
cyl('Leather handle',.015,.065,.25,leather)
cyl('Grip collar',.005,.081,.078,gold)
cyl('Pommel',-.17,.11,.14,gold)
sword=finish('GoldenSword',0)
# Shield with stepped crown, bevelled rim, curved-looking faceted face and rear grip.
outline=[(0,-.65),(.255,-.44),(.255,-.32),(.355,-.22),(.355,.39),(.21,.41),(.19,.53),(.10,.59),(-.10,.59),(-.19,.53),(-.21,.41),(-.355,.39),(-.355,-.22),(-.255,-.32),(-.255,-.44)]
inside=[(x*.77,z*.82) for x,z in outline]
slab('Shield body',outline,.085,darkgold,bevel=.014)
ring('Heavy golden rim',outline,inside,-.086,.075)
slab('Dark inset',inside,.045,dark,y=-.063,bevel=.008)
box('Vertical crest',0,-.01,.063,.91,.025,gold,y=-.104,b=.005)
for z in [-.37,.40]: box('Gold stud',0,z,.066,.075,.035,edge,y=-.107,b=.006)
for x in [-.178,.178]: box('Side stud',x,-.015,.058,.074,.032,gold,y=-.109,b=.006)
diamond=[(0,-.19),(.135,.02),(0,.20),(-.135,.02)]
slab('Diamond boss',diamond,.075,gold,y=-.137,bevel=.01)
gem('Shield crystal',0,.02,.142,.235,-.184)
for x in [-.10,.10]: box('Rear grip mount',x,0,.055,.22,.055,gold,y=.063)
box('Rear handle',0,0,.24,.044,.042,leather,y=.133)
shield=finish('GoldenShield',0)
# Spear: angular lance head, long octagonal shaft, three turquoise settings.
cyl('Octagonal shaft',.05,.034,1.95,dark)
for z in [-.89,-.29,.10,.51,.68]: cyl('Gold shaft ring',z,.046,.055,gold)
for z in [-.28,.62]:
 box('Spear jewel socket',0,z,.116,.108,.083,gold,b=.012)
 box('Socket recess',0,z,.064,.064,.01,darkgold,y=-.046,b=.001)
 gem('Shaft turquoise',0,z,.059,.059,-.055)
cyl('Butt cap',-1.025,.073,.18,gold)
box('Butt turquoise',0,-1.025,.039,.089,.01,cyan,y=-.069,b=.006)
head=[(-.065,.735),(.065,.735),(.113,.89),(0,1.235),(-.113,.89)]
slab('Lance head',head,.06,gold,bevel=.006)
inhead=[(-.026,.76),(.026,.76),(.073,.885),(0,1.18),(-.073,.885)]
ring('Spear polished edges',head,inhead,-.038,.025)
gem('Lance crystal',0,.884,.088,.18,-.065)
box('Lance throat',0,.765,.045,.094,.085,edge,b=.006)
spear=finish('GoldenSpear',.0)
# Studio source scene; individual mesh origins remain at attachment points.
for o,x in [(sword,-.82),(shield,0),(spear,.76)]: o.location.x=x
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.world.color=(.16,.16,.16)
def aim(o,p): o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for loc,power,size in [((-3,-4,5),650,4),((3,-2,3),450,3),((1,3,4),700,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc); o=bpy.context.object; o.data.energy=power; o.data.shape='DISK'; o.data.size=size; aim(o,(0,0,.2))
bpy.ops.object.camera_add(location=(2.3,-8,2.6)); camera=bpy.context.object; aim(camera,(0,0,.12)); camera.data.type='ORTHO'; camera.data.ortho_scale=3.15; scene.camera=camera
scene.render.resolution_x=1500; scene.render.resolution_y=1500; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'; scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(DOC,'GoldenArsenal_Preview.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(DOC,'GoldenArsenal.blend'))
bpy.ops.render.render(write_still=True)
camera.location=(0,-9,.12); aim(camera,(0,0,.12)); scene.render.filepath=os.path.join(DOC,'GoldenArsenal_Front.png'); bpy.ops.render.render(write_still=True)
with open(os.path.join(DOC,'mesh-report.json'),'w') as f: json.dump(models,f,indent=2)
