"""Build a small, independently attachable low-poly brass oil lamp. No Player assets touched."""
import bpy, math, random, json
from mathutils import Vector
from pathlib import Path

OUT = Path(__file__).resolve().parent / 'lamp-v1'
OUT.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(21)

def material(name, color, metal=0.0, rough=0.5, emission=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = rough
    if emission:
        p.inputs['Emission Color'].default_value = (1.0, 0.36, 0.035, 1)
        p.inputs['Emission Strength'].default_value = 0.0
    m.diffuse_color = (*color,1)
    return m

brass = [material('Brass_Warm',(.42,.235,.065),.82,.36),
         material('Brass_Light',(.49,.285,.085),.82,.38),
         material('Brass_Shade',(.34,.175,.046),.82,.42)]
trim = material('Brass_Edge',(.58,.355,.105),.86,.3)
dark = material('Recess_Dark',(.065,.035,.012),.35,.66)
glow = material('Magic_Inlay_Emission',(.30,.12,.025),.5,.42,True)

root = bpy.data.objects.new('Lamp_SwingPivot',None); bpy.context.collection.objects.link(root)
root.empty_display_size=.018
root['purpose']='Attach this object to a belt or hand socket. Rotate it for pendulum motion.'
root['units']='meters'
pivot=Vector((-.090,0,.136))
parts=[]
def mesh(name, verts, faces, mats, vary=False):
    d=bpy.data.meshes.new(name); d.from_pydata(verts,[],faces); d.update()
    o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o)
    o.parent=root; o.location=-pivot
    for m in mats: d.materials.append(m)
    if vary:
        for p in d.polygons: p.material_index=random.choices(range(len(mats)),[8,1,1])[0]
    parts.append(o); return o

def lathe(name, profile, mats, n=20, vary=False):
    verts=[(r*math.cos(2*math.pi*i/n),r*math.sin(2*math.pi*i/n),z) for r,z in profile for i in range(n)]
    faces=[]
    for j in range(len(profile)-1):
        for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    faces += [tuple(reversed(range(n))),tuple((len(profile)-1)*n+i for i in range(n))]
    return mesh(name,verts,faces,mats,vary)

lathe('Vessel',[(.032,.014),(.047,.025),(.061,.045),(.065,.065),(.059,.084),(.043,.097),(.031,.100)],brass,vary=True)
lathe('Foot',[(.037,0),(.042,.004),(.042,.009),(.031,.017)],brass,vary=True)
lathe('Lower_Band',[(.061,.045),(.063,.047),(.064,.051)], [trim])
lathe('Upper_Band',[(.065,.063),(.066,.066),(.064,.069)], [trim])
lathe('Dark_Decorative_Band',[(.063,.051),(.065,.063)],[dark])
lathe('Lid',[(.035,.099),(.039,.102),(.033,.107),(.021,.114),(.013,.115)],brass,vary=True)
lathe('Lid_Knob',[(.006,.115),(.006,.120),(.012,.124),(.009,.132),(.002,.135)],[trim],n=10)

# Tapered hollow spout. The dark inner wall and open mouth remain real geometry.
centers=[Vector(x) for x in [(.043,0,.062),(.075,0,.073),(.105,0,.093),(.140,0,.114),(.161,0,.123)]]
radii=[.022,.020,.015,.010,.009]
verts=[]; n=10
frames=[]
for j,c in enumerate(centers):
    tangent=(centers[min(j+1,len(centers)-1)]-centers[max(0,j-1)]).normalized()
    u=Vector((0,1,0)); v=tangent.cross(u).normalized(); frames.append((u,v))
    for i in range(n): verts.append(tuple(c+radii[j]*(u*math.cos(i*2*math.pi/n)+v*math.sin(i*2*math.pi/n))))
faces=[]
for j in range(len(centers)-1):
    for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
mesh('Spout',verts,faces,brass,True)
u,v=frames[-1]; c=centers[-1]; tangent=(centers[-1]-centers[-2]).normalized()
mouth=[]
for radius,depth in [(.009,0),(.0067,0),(.0055,-.019)]:
    mouth += [tuple(c+tangent*depth+radius*(u*math.cos(i*2*math.pi/n)+v*math.sin(i*2*math.pi/n))) for i in range(n)]
faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(2) for i in range(n)]
faces.append(tuple(reversed(range(2*n,3*n))))
o=mesh('Spout_Opening',mouth,faces,[trim,dark])
for p in o.data.polygons: p.material_index=0 if p.index<n else 1

# Oval handle in the side silhouette, joined to the vessel by two short mounts.
def tube(name, points, radius, mat, closed=False, sides=6):
    verts=[]
    for j,c in enumerate(points):
        c=Vector(c)
        tangent=(Vector(points[(j+1)%len(points)])-Vector(points[(j-1)%len(points)])).normalized() if closed else (Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])).normalized()
        u=Vector((0,1,0)); v=tangent.cross(u).normalized()
        for i in range(sides): verts.append(tuple(c+radius*(u*math.cos(2*math.pi*i/sides)+v*math.sin(2*math.pi*i/sides))))
    faces=[]
    for j in range(len(points) if closed else len(points)-1):
        for i in range(sides): faces.append((j*sides+i,j*sides+(i+1)%sides,((j+1)%len(points))*sides+(i+1)%sides,((j+1)%len(points))*sides+i))
    if not closed: faces += [tuple(reversed(range(sides))),tuple((len(points)-1)*sides+i for i in range(sides))]
    return mesh(name,verts,faces,[mat])
points=[(-.090+.034*math.cos(2*math.pi*i/16),0,.091+.045*math.sin(2*math.pi*i/16)) for i in range(16)]
tube('Handle',points,.0065,trim,True)
tube('Handle_Upper_Mount',[(-.055,0,.105),(-.038,0,.096)],.008,brass[0])
tube('Handle_Lower_Mount',[(-.065,0,.060),(-.050,0,.048)],.008,brass[0])

# Small diamond inlays: separate material, emission defaults OFF.
for i in range(12):
    a=2*math.pi*(i+.5)/12
    outward=Vector((math.cos(a),math.sin(a),0)); along=Vector((-math.sin(a),math.cos(a),0))
    c=outward*.0652+Vector((0,0,.057))
    verts=[tuple(c+Vector((0,0,.0045))),tuple(c+along*.0035),tuple(c-Vector((0,0,.0045))),tuple(c-along*.0035)]
    mesh('Inlay_%02d'%i,verts,[(3,2,1,0)],[glow])

def anchor(name,point,purpose):
    o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o)
    o.parent=root; o.location=Vector(point)-pivot; o.empty_display_size=.012; o['purpose']=purpose
    return o
anchor('BeltAttach',pivot,'Align this point to the belt hook. Coincides with swing pivot.')
anchor('HandGrip',(-.119,0,.096),'Reference point for hand socket alignment; adjust final grip to hand pose.')
anchor('FlameSocket',centers[-1],'Add optional flame VFX and light here in Unity.')
anchor('BodyCenter',(0,0,.060),'Approximate center for a collider and optional rigidbody center of mass.')
root['emission']='Magic_Inlay_Emission is separate; emission strength is zero. Enable emission and add a Light in Unity when desired.'

scene=bpy.context.scene; scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
# One mesh with material slots keeps runtime renderer count low; socket empties stay separate.
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
lamp_mesh=bpy.context.object; lamp_mesh.name='LampMesh'; parts=[lamp_mesh]
asset_objects=[root]+list(root.children)
bpy.ops.object.select_all(action='DESELECT')
for o in asset_objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.gltf(filepath=str(OUT/'Lamp.glb'),export_format='GLB',use_selection=True,export_extras=True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Lamp.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='COPY')

# Studio scene is saved for easy inspection; stage/lights are not exported.
floor_mat=material('Studio_Sand',(.17,.14,.105),0,.85)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-pivot.z-.001))
floor=bpy.context.object; floor.name='PREVIEW_Ground'; floor.data.materials.append(floor_mat)
world=bpy.data.worlds.new('Studio') if not scene.world else scene.world
scene.world=world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.32,.37,.43,1); world.node_tree.nodes['Background'].inputs[1].default_value=.45
target=Vector((.025,0,.067))-pivot
def area(name,loc,power,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=Vector(loc)-pivot;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
area('PREVIEW_Key',(.15,-.3,.47),12,.32)
area('PREVIEW_Fill',(-.3,-.15,.24),7,.25)
area('PREVIEW_Rim',(.02,.3,.34),16,.22)
d=bpy.data.cameras.new('PreviewCamera');camera=bpy.data.objects.new('PreviewCamera',d);scene.collection.objects.link(camera)
camera.location=Vector((.24,-.50,.25))-pivot;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=.39;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);bpy.context.view_layer.objects.active=root
scene.render.filepath=str(OUT/'Lamp-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Lamp.blend'))
bpy.ops.render.render(write_still=True)
stats={'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),'mesh_parts':len(parts),'length_m':.161-(-.1305),'height_m':.1425,'texture_images':0,'emission_default':'off','exports':['Lamp.blend','Lamp.fbx','Lamp.glb']}
(OUT/'lamp-info.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
print('LAMP_COMPLETE',json.dumps(stats))
