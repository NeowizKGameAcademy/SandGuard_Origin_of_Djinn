"""Editable 3D camera blocking test. This is not the final film's art or acting."""
import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector, Quaternion
ROOT=Path(r'C:/course/unity/SandGuard')
OUT=ROOT/'Docs/3.Ending/Video/production/rework'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene
s.render.engine='BLENDER_EEVEE'
s.render.resolution_x=1280; s.render.resolution_y=720; s.render.resolution_percentage=100
s.render.fps=24; s.frame_start=1; s.frame_end=192
s.world=bpy.data.worlds.new('Twilight'); s.world.use_nodes=True
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.085,.135,.22,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value=.42
s.view_settings.view_transform='AgX'
random.seed(36)
def mat(name,color,metal=0,rough=.65,emission=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    return m
stone=mat('Warm sandstone',(.27,.205,.135))
stones=[mat('Stone tone '+str(i),(.24+i*.017,.177+i*.014,.108+i*.011)) for i in range(5)]
dark=mat('Crack recess',(.055,.045,.039))
sand=mat('Distant dunes',(.155,.12,.09))
blue=mat('Crystal blue',(.025,.25,.58),.25,.17,1.4)
red=mat('Corruption seam',(.3,.004,.009),0,.3,2.0)
def box(name,loc,scale,m):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(m);be=o.modifiers.new('Stone softened corners','BEVEL');be.width=.035;be.segments=2
    o.modifiers.new('Normals','WEIGHTED_NORMAL');return o
def cyl(name,loc,r,depth,m,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc);o=bpy.context.object;o.name=name;o.data.materials.append(m);return o
box('Terrace foundation',(0,1,-.27),(18,16,.48),dark)
for x in range(-6,7):
    for y in range(-4,7):
        o=box('Floor slab %d %d'%(x,y),(x*1.22,y*1.22,-.035),(1.195,1.195,.07),random.choice(stones))
for x,y,h in [(3.05,2.7,4.1),(-4.5,3.7,3.1),(-4.5,-2.5,1.6),(4.8,-2.5,2.3),(-4.5,6.5,2.6),(4.8,6.5,4.1)]:
    cyl('Column foot',(x,y,.12),.63,.24,stone)
    cyl('Column shaft',(x,y,h/2+.24),.39,h,stone)
    cyl('Column top',(x,y,h+.25),.51,.22,stone)
for x in [-7.2,7.2]:
    for y in range(-4,8,2):box('Broken boundary',(x,y,.38),(1,1.8,.76),stone)
for i in range(20):
    x=random.uniform(-6.8,6.8);y=random.uniform(-4.5,7.7)
    if abs(x)<1.2:continue
    box('Rubble',(x,y,.08),(random.uniform(.12,.4),random.uniform(.1,.4),random.uniform(.08,.2)),stone)
for radius,z,depth in [(1.3,.14,.28),(1.05,.4,.24),(.8,.61,.18)]:cyl('Crystal pedestal',(-.65,5,z),radius,depth,stone)
verts=[(-.65,5,.93),(-1.23,4.65,1.55),(-.12,4.60,1.72),(.02,5.37,1.52),(-1.04,5.53,1.68),(-.48,5,3.50)]
faces=[(0,2,1),(0,3,2),(0,4,3),(0,1,4),(1,2,5),(2,3,5),(3,4,5),(4,1,5)]
mesh=bpy.data.meshes.new('Crystal facets');mesh.from_pydata(verts,[],faces);mesh.update()
crystal=bpy.data.objects.new('Crystal',mesh);s.collection.objects.link(crystal);crystal.data.materials.append(blue)
for i in range(3):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.16,location=(-.65+math.cos(i*2.09)*1.3,5+math.sin(i*2.09)*1.2,1.7+i*.18))
    bpy.context.object.data.materials.append(blue)
box('Desert ground',(0,20,-1),(180,180,.5),sand)
for i in range(10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=10,radius=1,location=(random.uniform(-65,65),random.uniform(22,70),-3))
    o=bpy.context.object;o.name='Dune';o.scale=(random.uniform(12,23),random.uniform(7,16),random.uniform(4,8));o.data.materials.append(sand)
    for p in o.data.polygons:p.use_smooth=True
def light(name,kind,loc,color,power,size=1,target=(0,1,1)):
    d=bpy.data.lights.new(name,kind);d.energy=power;d.color=color
    if kind=='AREA':d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
light('Warm surviving torch','AREA',(3,2,5),(1,.59,.27),850,5)
light('Moon fill','AREA',(-4,-1,6),(.31,.5,1),1100,7)
light('Crystal spill','POINT',(-.65,4.2,2.6),(.10,.48,1),140,2)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Player/Art/Protagonist/Walk.fbx'))
arm=next(o for o in s.objects if o.type=='ARMATURE')
actor=next(o for o in s.objects if o.type=='MESH' and 'tripo' in o.name)
samples=[]
for f in range(1,37):
    s.frame_set(f);samples.append({b.name:b.matrix_basis.copy() for b in arm.pose.bones})
arm.animation_data_clear()
p=actor.data.materials[0].node_tree.nodes.get('Principled BSDF')
nodes=actor.data.materials[0].node_tree.nodes;links=actor.data.materials[0].node_tree.links
tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'Assets/Player/Art/Protagonist/Protagonist_BaseColor.jpg'));tex.image.pack()
links.new(tex.outputs['Color'],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.75
rigroot=bpy.data.objects.new('Actor blocking root',None);s.collection.objects.link(rigroot)
arm.parent=rigroot;rigroot.scale=(1.82,1.82,1.82);rigroot.rotation_euler.z=math.pi
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Player/Art/Lamp/Lamp.fbx'))
lamp=bpy.data.objects['LampMesh'];lamp.data.transform(lamp.matrix_world);lamp.parent=None;lamp.matrix_world.identity()
for v in lamp.data.vertices:v.co*=1.35
for m in lamp.data.materials:
    if m.use_nodes:
        pm=m.node_tree.nodes.get('Principled BSDF')
        if pm:pm.inputs['Metallic'].default_value=.7;pm.inputs['Roughness'].default_value=.36
camd=bpy.data.cameras.new('35mm camera');cam=bpy.data.objects.new('Camera lateral arc',camd);s.collection.objects.link(cam);s.camera=cam
camd.lens=35;camd.sensor_width=36;camd.dof.use_dof=True;camd.dof.aperture_fstop=5.6
def smooth(x):return x*x*(3-2*x)
trajectory=[]
for f in range(1,193):
    t=(f-1)/191;sec=(f-1)/24
    walksec=min(sec,4.55);ix=int(walksec*24)%36
    if sec>=4.55:ix=35
    for b in arm.pose.bones:
        b.matrix_basis=samples[ix][b.name];b.rotation_mode='QUATERNION'
        if b.name=='mixamorig:Head' and sec>4.55:
            b.rotation_quaternion=b.rotation_quaternion@Quaternion((0,1,0),-.14*smooth(min(1,(sec-4.55)/1.5)))
        for prop in ['location','rotation_quaternion','scale']:b.keyframe_insert(prop,frame=f)
    rigroot.location=(0,walksec*.38,0);rigroot.keyframe_insert('location',frame=f)
    bpy.context.view_layer.update()
    hand=arm.matrix_world@arm.pose.bones['mixamorig:LeftHand'].matrix
    lamp.location=hand.translation+Vector((-.05,.02,0));lamp.rotation_euler=(0,0,math.pi);lamp.keyframe_insert('location',frame=f)
    a=math.radians(48-100*smooth(t));radius=4.65
    cam.location=(math.cos(a)*radius,math.sin(a)*radius+.25,1.12+.68*smooth(t))
    reveal=smooth(max(0,min(1,(t-.48)/.52)))
    target=Vector((-.3*reveal,rigroot.location.y+1.45*reveal,1.08+.9*reveal))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    camd.dof.focus_distance=(Vector((-.65,5,2.1)) if reveal>.65 else Vector((0,rigroot.location.y,1.2))).__sub__(cam.location).length
    cam.keyframe_insert('location',frame=f);cam.keyframe_insert('rotation_euler',frame=f);camd.keyframe_insert('dof.focus_distance',frame=f)
    if f in [1,49,97,145,192]:trajectory.append(dict(frame=f,camera=list(cam.location),target=list(target),actor=list(rigroot.location)))
for a in bpy.data.actions:
    for layer in a.layers:
        for strip in layer.strips:
            for slot in a.slots:
                bag=strip.channelbag(slot,ensure=False)
                if bag:
                    for curve in bag.fcurves:
                        for k in curve.keyframe_points:k.interpolation='LINEAR'
s.render.image_settings.file_format='PNG'
s.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Camera-Study-8s.blend'))
(OUT/'camera-trajectory.json').write_text(json.dumps(dict(purpose='Camera blocking only; not final character acting, prop grip or production art',fps=24,frames=192,focalLength=35,trajectory=trajectory),indent=2),encoding='utf-8')
for f in [1,72,144,192]:
    s.frame_set(f);s.render.filepath=str(OUT/f'preview-{f:03}.png');bpy.ops.render.render(write_still=True)
if '--render' in sys.argv:
    (OUT/'frames').mkdir(exist_ok=True)
    s.render.filepath=str(OUT/'frames/f_')
    bpy.ops.render.render(animation=True)
