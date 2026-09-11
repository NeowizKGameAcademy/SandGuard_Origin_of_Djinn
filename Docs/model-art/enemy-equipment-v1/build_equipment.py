"""Deterministic Blender equipment authoring. Run in Blender background mode."""
import bpy, bmesh, math, json, random
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
EXPORT=ROOT/'Exports'; PREVIEW=ROOT/'Previews'
EXPORT.mkdir(exist_ok=True); PREVIEW.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
scene.render.fps=24; scene.frame_start=1; scene.frame_end=49
random.seed(91)
# Shared palette: iron, steel, timber, leather, brass and crimson.
COLORS=['302b2a','514b48','716c65','9b9990','c4c1ae','3c2920','62422b','89603a',
        'a37a48','261e1b','493125','765036','805421','bb8433','e5b34c','f2cf72',
        '4b1518','792122','9e302a','bc4333','ce5a3c','382329','d5b98a','ecdbb0']
image=bpy.data.images.new('EquipmentPalette',width=128,height=128,alpha=True)
px=[]
for y in range(128):
    for x in range(128):
        n=min((y//16)*8+x//16,len(COLORS)-1)
        h=COLORS[n]; px.extend([int(h[i:i+2],16)/255 for i in (0,2,4)]+[1])
image.pixels=px; image.filepath_raw=str(EXPORT/'EquipmentPalette.png'); image.file_format='PNG'; image.save(); image.pack()
mat=bpy.data.materials.new('EquipmentPalette'); mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.77
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])

class Mesh:
    def __init__(self): self.v=[]; self.f=[]; self.c=[]
    def face(self,ids,c): self.f.append(ids); self.c.append(c)
    def box(self,center,size,c):
        x,y,z=center; a,b,d=[s/2 for s in size]; k=len(self.v)
        self.v.extend([(x+sx*a,y+sy*b,z+sz*d) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]])
        for face in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]: self.face([k+i for i in face],c)
    def slab(self,outline,y,thickness,c):
        k=len(self.v); n=len(outline)
        self.v.extend([(x,y-thickness/2,z) for x,z in outline]+[(x,y+thickness/2,z) for x,z in outline])
        self.face(list(range(k,k+n))[::-1],c); self.face(list(range(k+n,k+2*n)),c)
        for i in range(n): j=(i+1)%n; self.face([k+i,k+j,k+n+j,k+n+i],c)
    def finish(self,name,parent):
        mesh=bpy.data.meshes.new(name); mesh.from_pydata(self.v,[],self.f); mesh.update()
        obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); obj.parent=parent
        mesh.materials.append(mat)
        uv=mesh.uv_layers.new(name='PaletteUV')
        for poly,c in zip(mesh.polygons,self.c):
            p=((c%8+.5)/8,(c//8+.5)/8)
            for li in poly.loop_indices: uv.data[li].uv=p
        bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(mesh); bm.free()
        return obj

assets=[]
def empty(name,parent=None,loc=(0,0,0)):
    o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o); o.parent=parent; o.location=loc
    o.empty_display_size=.055; return o
def asset(name):
    col=bpy.data.collections.new(name); scene.collection.children.link(col)
    layer=bpy.context.view_layer.layer_collection.children[col.name]; bpy.context.view_layer.active_layer_collection=layer
    root=empty(name); entry={'name':name,'root':root,'collection':col}; assets.append(entry); return root
def socket(name,root,point): return empty(name,root,point)
def grip(m,length=.21,gold=True):
    m.box((0,0,0),(.065,.065,length),9)
    count=7
    for i in range(count): m.box((0,0,-length/2+(i+.5)*length/count),(.077,.073,length/count*.8),10 if i%2 else 11)
    m.box((0,0,-length/2-.025),(.105,.09,.05),13 if gold else 2)
    m.box((0,-.048,-length/2-.025),(.044,.016,.031),14 if gold else 3)
def crossguard(m,z,width=.28,gold=True):
    c=13 if gold else 2
    m.box((0,0,z),(width,.09,.055),c)
    for sign in [-1,1]:
        m.box((sign*(width/2-.024),0,z+.015),(.057,.105,.09),14 if gold else 3)
    m.box((0,-.051,z),(.063,.014,.043),14 if gold else 3)
def blade(m,rows,gold_edge=False):
    # Closed bevel-ridged blade, segmented for square color patches.
    k=len(m.v)
    for z,c,w in rows:
        m.v.extend([(c-w/2,0,z),(c,-.024,z),(c+w/2,0,z),(c,.024,z)])
    for r in range(len(rows)-1):
        for j in range(4):
            n=(j+1)%4
            m.face([k+r*4+j,k+r*4+n,k+(r+1)*4+n,k+(r+1)*4+j],(14 if gold_edge and j in (0,3) else [4,2,3,3][j]) if r%3 else (13 if gold_edge and j in (0,3) else [3,1,2,2][j]))
    m.face([k+3,k+2,k+1,k],2); m.face([k+4*(len(rows)-1)+i for i in range(4)],4)
    # Narrow stepped inlaid edge strip on the show face.
    for (z,c,w),(z2,c2,w2) in zip(rows[:-2],rows[1:-1]):
        if gold_edge:
            m.slab([(c-w/2,z),(c-w/2+.018,z),(c2-w2/2+.018,z2),(c2-w2/2,z2)],-.008,.009,14)

root=asset('ShortSword'); m=Mesh(); grip(m); crossguard(m,.14)
blade(m,[(.17+i*.07,0,.115 if i<6 else .115-(i-5)*.019) for i in range(10)]+[(.87,0,.003)])
obj=m.finish('ShortSword_Mesh',root); socket('HandGrip',root,(0,0,0)); socket('BladeTip',root,(0,0,.87))

root=asset('AssassinDagger'); m=Mesh(); grip(m,.17,False); crossguard(m,.115,.21,False)
blade(m,[(.15+i*.045,-.002*i*i,.09 if i<5 else .09-(i-4)*.022) for i in range(8)]+[(.52,-.13,.003)])
m.finish('AssassinDagger_Mesh',root); socket('HandGrip',root,(0,0,0)); socket('BladeTip',root,(-.13,0,.52))

root=asset('ChiefScimitar'); m=Mesh(); grip(m,.24); crossguard(m,.16,.29)
blade(m,[(.20+i*.068,-.0027*i*i,.105+i*.004 if i<8 else .137-(i-8)*.029) for i in range(13)],True)
m.finish('ChiefScimitar_Mesh',root); socket('HandGrip',root,(0,0,0)); socket('BladeTip',root,(-.3888,0,1.016))

root=asset('Warhammer'); m=Mesh(); m.box((0,0,.22),(.063,.063,.91),6)
for i in range(9): m.box((0,0,-.16+i*.028),(.081,.078,.021),10 if i%2 else 11)
for z in [-.23,.105,.43,.64]: m.box((0,0,z),(.107,.103,.042),2)
m.box((0,0,.73),(.58,.23,.25),1)
for side in [-1,1]:
    m.box((side*.215,0,.73),(.19,.26,.29),2)
    m.box((side*.31,0,.73),(.035,.22,.24),3)
    for a in range(3):
        for b in range(3):
            m.box((side*.215+(a-1)*.055,-.135,.73+(b-1)*.077),(.052,.012,.073),random.choice([1,2,2,3]))
m.box((0,0,.73),(.115,.278,.31),18)
for z in [.66,.80]: m.box((0,-.148,z),(.029,.018,.029),3)
m.finish('Warhammer_Mesh',root); socket('HandGrip',root,(0,0,0)); socket('OffHandGrip',root,(0,0,.36)); socket('ImpactCenter',root,(.329,0,.73))

def shield_back(m,width,height):
    # Handle and two raised support rails are on rear (+Y), grip midpoint is origin.
    for z in [-height*.26,height*.26]: m.box((0,-.045,z),(width*.82,.036,.04),10)
    for x in [-.105,.105]: m.box((x,-.025,0),(.03,.105,.055),2)
    m.box((0,.017,0),(.24,.055,.055),10)
    for x in [-.08,-.04,0,.04,.08]: m.box((x,.02,0),(.022,.061,.061),11)

root=asset('RoundShield'); m=Mesh(); step=.045; radius=.32
# A watertight exposed-surface voxel shell avoids hidden cube faces.
cells={}
for i in range(-7,7):
    for j in range(-7,7):
        x=(i+.5)*step; z=(j+.5)*step
        if x*x+z*z<=radius*radius: cells[(i,j)]=math.hypot(x,z)
for (i,j),r in cells.items():
    x=(i+.5)*step; z=(j+.5)*step; rim=any((i+a,j+b) not in cells for a,b in [(1,0),(-1,0),(0,1),(0,-1)])
    c=random.choice([1,2,3]) if rim else [6,7,8,7,6][i%5]
    y=-.105-(.018 if rim else 0)
    # Closed individual blocks, joined into a single renderer.
    m.box((x,y,z),(step*.998,.075 if rim else .045,step*.998),c)
for angle in [i*math.tau/8 for i in range(8)]:
    m.box((math.cos(angle)*.269,-.157,math.sin(angle)*.269),(.032,.025,.032),3)
outline=[(.105*math.cos(i*math.tau/8),.105*math.sin(i*math.tau/8)) for i in range(8)]
m.slab(outline,-.147,.044,1)
m.slab([(.074*math.cos(i*math.tau/8),.074*math.sin(i*math.tau/8)) for i in range(8)],-.183,.057,3)
shield_back(m,.6,.6); m.finish('RoundShield_Mesh',root)
socket('HandGrip',root,(0,0,0)); socket('ShieldFace',root,(0,-.22,0))

root=asset('TowerShield'); m=Mesh(); width=.56; height=1.13
for i in range(8):
    x=(i-3.5)*.066; top=.565-[0,.018,.03,0,.015,.026,.009,.04][i]
    m.box((x,-.11,(top-.565)/2),(.064,.065,top+.565),[6,7,8,7][i%4])
    for j in range(14):
        z=-.52+j*.077
        if z>top-.04: continue
        if (i+j*2)%9<4: m.box((x,-.146,z),(.061,.008,.072),random.choice([16,17,18]))
for side in [-1,1]:
    for j in range(6):
        z=-.47+j*.19
        m.box((side*.254,-.147,z),(.066,.036,.18),2 if j%2 else 3)
        m.box((side*.254,-.173,z),(.025,.02,.025),1)
for z in [-.55,-.19,.26,.51]:
    m.box((0,-.151,z),(.54,.035,.04),2)
    for x in [-.19,.19]: m.box((x,-.176,z),(.027,.018,.027),3)
m.slab([(-.07,.14),(.11,.14),(.11,.42),(-.07,.42)],-.165,.025,2)
for z in [.19,.36]: m.box((.02,-.189,z),(.028,.02,.028),3)
shield_back(m,.56,1.1); m.finish('TowerShield_Mesh',root)
socket('HandGrip',root,(0,0,0)); socket('ShieldFace',root,(0,-.20,0))

# Closed thin draped cloth, 3 four-bone chains with smooth normalized weights.
root=asset('ChiefCape'); m=Mesh(); NX=18; NZ=20; length=1.18
def surface(u,t):
    half=.325+.22*t
    x=u*half
    y=.055+.20*t+.055*(1-u*u)+.037*math.cos(u*math.pi*3)*(.2+.8*t)
    hem=.026*(int((u+1)*9)%3) if t==1 else 0
    return (x,y,-length*t+hem)
for layer in range(2):
    for j in range(NZ+1):
        for i in range(NX+1):
            x,y,z=surface(2*i/NX-1,j/NZ); m.v.append((x,y+layer*.012,z))
N=(NX+1)*(NZ+1)
for layer in range(2):
    for j in range(NZ):
        for i in range(NX):
            k=layer*N+j*(NX+1)+i
            c=13 if (i in (0,NX-1) and j<NZ-1) else random.choices([16,17,18,19],[1,4,6,1])[0]
            ids=[k,k+1,k+NX+2,k+NX+1]; m.face(ids if layer else ids[::-1],c if layer else max(16,c) if c!=13 else c)
edge=list(range(NX+1))+[j*(NX+1)+NX for j in range(1,NZ+1)]+[NZ*(NX+1)+i for i in range(NX-1,-1,-1)]+[j*(NX+1) for j in range(NZ-1,0,-1)]
for a,b in zip(edge,edge[1:]+edge[:1]): m.face([a,b,b+N,a+N],17)
cape=m.finish('ChiefCape_Mesh',root)
armdata=bpy.data.armatures.new('ChiefCape_Skeleton'); arm=bpy.data.objects.new('ChiefCape_Rig',armdata); bpy.context.collection.objects.link(arm); arm.parent=root
bpy.context.view_layer.objects.active=arm; arm.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
bone=armdata.edit_bones.new('CapeRoot'); bone.head=(0,0,0); bone.tail=(0,0,.12)
for column,u in [('L',-.72),('C',0),('R',.72)]:
    parent=bone
    for j in range(4):
        b=armdata.edit_bones.new(f'Cape_{column}_{j+1:02}'); b.head=surface(u,j/4); b.tail=surface(u,(j+1)/4); b.parent=parent; b.use_connect=j>0; parent=b
bpy.ops.object.mode_set(mode='OBJECT'); arm.select_set(False)
groups={b.name:cape.vertex_groups.new(name=b.name) for b in armdata.bones}
for idx in range(len(cape.data.vertices)):
    k=idx%N; j=k//(NX+1); i=k%(NX+1); u=2*i/NX-1; t=j/NZ
    if j==0: groups['CapeRoot'].add([idx],1,'REPLACE'); continue
    p=max(0,min(2,(u+.72)/.72)); a=min(1,int(p)); b=a+1; blend=p-a
    v=max(0,min(3,t*4-.5)); c=min(2,int(v)); d=c+1; mix=v-c
    rootweight=max(0,1-t*8)
    if rootweight: groups['CapeRoot'].add([idx],rootweight,'REPLACE')
    for col,w in [(a,1-blend),(b,blend)]:
        for row,q in [(c,1-mix),(d,mix)]:
            weight=w*q*(1-rootweight)
            if weight>0: groups[f'Cape_{["L","C","R"][col]}_{row+1:02}'].add([idx],weight,'REPLACE')
mod=cape.modifiers.new('Cape skin','ARMATURE'); mod.object=arm
cape.parent=arm
arm.animation_data_create()
for frame in range(1,50,4):
    phase=(frame-1)/48*math.tau
    for b in arm.pose.bones:
        b.rotation_mode='XYZ'
        if b.name=='CapeRoot': continue
        level=int(b.name[-2:]); col={'L':-.5,'C':0,'R':.5}[b.name.split('_')[1]]
        b.rotation_euler=(math.radians(4+level*1.6)*math.sin(phase-level*.45+col),0,math.radians(2)*math.sin(phase+col))
        b.keyframe_insert('rotation_euler',frame=frame,group=b.name)
arm.animation_data.action.name='Cape_Idle_Sway'
scene.frame_set(1)
socket('ShoulderAttach',root,(0,0,0))
assets[-1]['rig']=arm

report={'units':'meters','blender_axes':'X right, -Y front, Z up','palette':'EquipmentPalette.png','assets':[]}
for entry in assets:
    name=entry['name']; objs=list(entry['collection'].objects)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=entry['root']
    # FBX exporter records rest matrices itself. POSE is required to bake motion.
    if 'rig' in entry: entry['rig'].data.pose_position='POSE'
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim='rig' in entry,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,path_mode='COPY',embed_textures=False)
    if 'rig' in entry: entry['rig'].data.pose_position='POSE'
    bpy.ops.export_scene.gltf(filepath=str(EXPORT/(name+'.glb')),export_format='GLB',use_selection=True,export_animations='rig' in entry,export_skins=True)
    meshes=[o for o in objs if o.type=='MESH']
    triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
    pts=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
    entry['dimensions']=[max(p[i] for p in pts)-min(p[i] for p in pts) for i in range(3)]
    report['assets'].append({'name':name,'triangles':triangles,'mesh_objects':len(meshes),'dimensions_xyz_m':entry['dimensions'],'bones':len(entry['rig'].data.bones) if 'rig' in entry else 0,'sockets':[o.name for o in objs if o.type=='EMPTY' and o!=entry['root']]})

# Studio preview, staged originals; exports above remain at attachment origin.
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection
for entry,pos in zip(assets,[(-2.8,0,.65),(-1.9,0,.65),(-1.0,0,.65),(.15,0,.65),(-2.35,0,-.40),(-.95,0,-.43),(1.75,0,1.26)]):
    entry['root'].location=pos
    if entry['name']=='ChiefCape': entry['root'].rotation_euler.z=math.pi
def plain(name,color):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); return m
ink=plain('Preview_Ink',(.82,.76,.61))
def label(text,loc,size=.075):
    cu=bpy.data.curves.new(text,'FONT'); cu.body=text; cu.size=size; cu.align_x='CENTER'; cu.extrude=0
    o=bpy.data.objects.new(text,cu); scene.collection.objects.link(o); o.location=loc; o.rotation_euler=(math.pi/2,0,0); cu.materials.append(ink)
for text,pos in [('01  SHORT SWORD',(-2.8,-.22,.38)),('02  DAGGER',(-1.9,-.22,.38)),('03  SCIMITAR',(-1,-.22,.38)),('04  WARHAMMER',(.15,-.22,.32)),('05  ROUND SHIELD',(-2.35,-.22,-.87)),('06  TOWER SHIELD',(-.95,-.22,-1.12)),('07  CHIEF CAPE  /  13 BONES',(1.75,-.4,-.16))]: label(text,pos)
label('SANDGUARD  /  ENEMY EQUIPMENT',(-.45,-.12,1.98),.12)
label('MODELED ASSETS     -     SEPARATE ATTACHMENTS     -     METERS',(-.45,-.12,1.80),.054)
world=bpy.data.worlds.new('Studio'); scene.world=world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.065,.08,.12,1); world.node_tree.nodes['Background'].inputs[1].default_value=.45
def area(name,loc,power,size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    o=bpy.data.objects.new(name,data); scene.collection.objects.link(o); o.location=loc; o.rotation_euler=(Vector((0,0,.6))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-3,-4,5),650,5); area('Fill',(4,-2,2),450,4); area('Rim',(0,3,4),700,3)
camdata=bpy.data.cameras.new('Equipment_Camera'); cam=bpy.data.objects.new('Equipment_Camera',camdata); scene.collection.objects.link(cam); scene.camera=cam
cam.location=(-.45,-10,3.0); target=Vector((-.45,0,.45)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); camdata.type='ORTHO'; camdata.ortho_scale=6.4
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.render.resolution_x=1800; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(PREVIEW/'Equipment_Overview.png')
scene.render.film_transparent=False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'EnemyEquipment.blend'))
(ROOT/'equipment_manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.render.render(write_still=True)
print('EQUIPMENT_COMPLETE',json.dumps(report))
