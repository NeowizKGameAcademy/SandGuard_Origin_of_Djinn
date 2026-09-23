"""Full 49-second 3D previs: editable blocking, cameras, proxy FX and shot markers."""
import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix
ROOT=Path(r'C:/course/unity/SandGuard');OUT=Path(__file__).resolve().parent
OUT.mkdir(exist_ok=True);(OUT/'qa').mkdir(exist_ok=True);random.seed(20260923)
SHOTS=[('R01',0,4.5,'불길한 진동'),('R02',4.5,6,'마석 균열'),('R03',6,9,'신전 밖으로 번지는 재앙'),('R04',9,12,'마을의 위기'),('R05',12,15,'손끝의 대가'),('R06',15,16.5,'깨달음'),('R07',16.5,19,'손을 거두자 되살아나는 재앙'),('R08',19,23,'마지막으로 바라보는 사막'),('R09',23,28,'마지막 소원'),('R10',28,30.5,'이번에는 놓지 않는다'),('R11',30.5,36.5,'영혼으로 붙드는 봉인'),('R12',36.5,39,'마지막 시선'),('R13',39,42,'남겨진 램프'),('R14',42,45.5,'돌아온 자유'),('R15',45.5,47,'램프 안의 의식'),('R16',47,49,'첫 번째 지니')]
def clamp(x):return max(0,min(1,x))
def ease(x):x=clamp(x);return x*x*(3-2*x)
def mix(a,b,t):return a+(b-a)*t
def vmix(a,b,t):return Vector(a).lerp(Vector(b),t)
def sample_file(file,frames):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(ROOT/file))
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');out=[]
    for f in frames:
        bpy.context.scene.frame_set(f);out.append({b.name:b.matrix_basis.copy() for b in arm.pose.bones})
    return out
idle=sample_file('Assets/Player/Art/Protagonist/Protagonist.fbx',[1])[0]
crouch=sample_file('Assets/Player/Art/Protagonist/MobilityAnimations/CrouchPose.fbx',[1])[0]
walk=sample_file('Assets/Player/Art/Protagonist/Walk.fbx',range(1,37))
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Docs/3.Ending/Video/production/rework/Camera-Study-8s.blend'))
s=bpy.context.scene
for o in s.objects:
    o.animation_data_clear()
    if o.data and hasattr(o.data,'animation_data_clear'):o.data.animation_data_clear()
s.timeline_markers.clear();s.frame_start=1;s.frame_end=1176;s.render.fps=24
s.render.resolution_x=1280;s.render.resolution_y=720;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.render.engine='BLENDER_EEVEE'
if hasattr(s,'eevee') and hasattr(s.eevee,'taa_render_samples'):s.eevee.taa_render_samples=24
arm=bpy.data.objects['Armature'];actor=next(o for o in s.objects if o.type=='MESH' and 'tripo' in o.name)
rig=bpy.data.objects['Actor blocking root'];lamp=bpy.data.objects['LampMesh'];lamp.rotation_mode='XYZ'
base_mat=actor.data.materials[0];base_mat.name='Hero cloth and skin'
for o in list(s.objects):
    if o.type=='CAMERA':bpy.data.objects.remove(o,do_unlink=True)
def material(name,color,metal=0,rough=.7,emission=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    return m
gold=material('Film lamp bronze',(.34,.19,.055),.78,.3)
bronze_dark=material('Lamp dark recess',(.08,.034,.01),.5,.4)
cyan=material('Blue magic',(.035,.40,1),.1,.25,1.2)
red=material('Corruption crimson',(.27,.005,.012),0,.65,1.4)
smoke=material('Shadow current',(.021,.009,.014),0,.8,.08)
stone=bpy.data.materials['Warm sandstone'];sand=bpy.data.materials['Distant dunes']
skin=material('Guardian warm skin',(.36,.19,.10));cloth=material('Guardian teal robe',(.045,.20,.20))
childcloth=material('Child ochre tunic',(.48,.24,.07));hair=material('Grey hair',(.3,.28,.25))
wood=material('Old door wood',(.10,.052,.028));brick=material('Village adobe',(.32,.23,.15))
black=material('Shadow',(.005,.007,.01));eyemat=material('Eyes',(.015,.009,.006))
for slot in lamp.material_slots:slot.material=bronze_dark if 'Recess' in slot.name else gold
def sphere(name,loc,scale,mat,segments=16,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=loc)
    o=bpy.context.object;o.name=name;o.scale=scale;o.data.materials.append(mat)
    for p in o.data.polygons:p.use_smooth=True
    return o
def box(name,loc,scale,mat,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    if bevel:q=o.modifiers.new('Soft edges','BEVEL');q.width=bevel;q.segments=2;o.modifiers.new('Normals','WEIGHTED_NORMAL')
    return o
def empty(name):
    o=bpy.data.objects.new(name,None);s.collection.objects.link(o);return o
def curve(name,points,mat,width):
    d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.resolution_u=2;d.bevel_resolution=2;d.bevel_depth=width
    sp=d.splines.new('POLY');sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points):p.co=(*co,1)
    o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.data.materials.append(mat);return o
def light(name,kind,loc,color,power,size=4):
    d=bpy.data.lights.new(name,kind);d.color=color;d.energy=power
    if kind=='AREA':d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=loc
    o.rotation_euler=(Vector((loc[0],loc[1],0))-o.location).to_track_quat('-Z','Y').to_euler();return o
def key(o,prop,f):o.keyframe_insert(prop,frame=f)
def trs_point(p,origin,yaw):return Vector(origin)+Matrix.Rotation(yaw,3,'Z')@Vector(p)
def local_to_world(p):return trs_point(p,rig.location,rig.rotation_euler.z)
def worlddir(v):return Matrix.Rotation(rig.rotation_euler.z,3,'Z')@Vector(v)
# A front rim gives the desert-view sequence foreground depth.
for x in [-5.5,-3.3,2.3,4.5,6.5]:box('Southern parapet',(x,-3.4,.48),(1.7,.46,.96),stone)
box('Broken viewing ledge',(-.1,-3.4,.19),(1.65,.47,.38),stone)
for x,y,sx,sy,sz in [(21,-25,15,9,2.6),(44,-36,21,12,4.0),(65,-48,24,15,5.0),(9,-47,22,12,3.2),(39,-68,29,19,6),(83,-70,31,20,7)]:
    sphere('Southern desert dune',(x,y,-1.1),(sx,sy,sz),sand,32,16)
moon=light('Desert moon fill','SUN',(20,-20,40),(.35,.49,.75),.32)
moon.rotation_euler=(math.radians(25),math.radians(-18),math.radians(-35));moon.data.angle=.2
# Front emblem, deliberately readable in the hand coverage.
mesh=bpy.data.meshes.new('Lamp star emblem mesh')
mesh.from_pydata([(.09,.10,-.061),(.11,.10,-.087),(.09,.10,-.113),(.07,.10,-.087)],[],[(0,1,2,3)]);mesh.update()
badge=bpy.data.objects.new('Lamp seal emblem',mesh);s.collection.objects.link(badge);badge.parent=lamp;badge.data.materials.append(cyan)
sealglow=light('Seal light','POINT',(0,0,1),(.04,.4,1),0);sealglow.data.shadow_soft_size=.15
# Hero arms have explicit world-space wrist targets; the lamp stays rigid.
hands={}
for side in ['Left','Right']:
    target=empty(side+' wrist target');pole=empty(side+' elbow guide');rotation=empty(side+' hand orientation')
    ik=arm.pose.bones['mixamorig:'+side+'ForeArm'].constraints.new('IK');ik.name='Film arm IK';ik.target=target;ik.pole_target=pole;ik.chain_count=2;ik.use_stretch=False
    co=arm.pose.bones['mixamorig:'+side+'Hand'].constraints.new('COPY_ROTATION');co.target=rotation;co.target_space='WORLD';co.owner_space='WORLD'
    hands[side]=(target,pole,rotation,ik,co)
# Actual hand polygons glow; a height/noise front progressively removes the body.
def dissolve_shader(m,is_hand=False):
    n=m.node_tree.nodes;l=m.node_tree.links;out=n.get('Material Output');base=n.get('Principled BSDF')
    geom=n.new('ShaderNodeNewGeometry');sep=n.new('ShaderNodeSeparateXYZ');l.new(geom.outputs['Position'],sep.inputs[0])
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=18;l.new(geom.outputs['Position'],noise.inputs['Vector'])
    mult=n.new('ShaderNodeMath');mult.operation='MULTIPLY';mult.inputs[1].default_value=.07;l.new(noise.outputs['Fac'],mult.inputs[0])
    height=n.new('ShaderNodeMath');height.operation='ADD';l.new(sep.outputs['Z'],height.inputs[0]);l.new(mult.outputs[0],height.inputs[1])
    limit=n.new('ShaderNodeValue');limit.name='Dissolve height';limit.outputs[0].default_value=-1
    test=n.new('ShaderNodeMath');test.operation='LESS_THAN';l.new(height.outputs[0],test.inputs[0]);l.new(limit.outputs[0],test.inputs[1])
    delta=n.new('ShaderNodeMath');delta.operation='SUBTRACT';l.new(height.outputs[0],delta.inputs[0]);l.new(limit.outputs[0],delta.inputs[1])
    absolute=n.new('ShaderNodeMath');absolute.operation='ABSOLUTE';l.new(delta.outputs[0],absolute.inputs[0])
    edge=n.new('ShaderNodeMath');edge.operation='LESS_THAN';edge.inputs[1].default_value=.055;l.new(absolute.outputs[0],edge.inputs[0])
    emission=n.new('ShaderNodeEmission');emission.inputs[0].default_value=(.02,.45,1,1);emission.inputs[1].default_value=1.6
    glow=n.new('ShaderNodeMixShader');l.new(edge.outputs[0],glow.inputs[0]);l.new(base.outputs[0],glow.inputs[1]);l.new(emission.outputs[0],glow.inputs[2])
    handval=n.new('ShaderNodeValue');handval.name='Hand price glow';handval.outputs[0].default_value=0
    handmix=n.new('ShaderNodeMixShader');l.new(handval.outputs[0],handmix.inputs[0]);l.new(glow.outputs[0],handmix.inputs[1]);l.new(emission.outputs[0],handmix.inputs[2])
    trans=n.new('ShaderNodeBsdfTransparent');end=n.new('ShaderNodeMixShader');l.new(test.outputs[0],end.inputs[0]);l.new(handmix.outputs[0] if is_hand else glow.outputs[0],end.inputs[1]);l.new(trans.outputs[0],end.inputs[2]);l.new(end.outputs[0],out.inputs['Surface'])
    if hasattr(m,'surface_render_method'):m.surface_render_method='DITHERED'
    return limit,handval
handmat=base_mat.copy();handmat.name='Right fingertips price';actor.data.materials.append(handmat)
ids={g.index for g in actor.vertex_groups if 'RightHand' in g.name and g.name!='mixamorig:RightHand'}
weights={v.index:sum(g.weight for g in v.groups if g.group in ids) for v in actor.data.vertices}
for p in actor.data.polygons:
    if sum(weights[v] for v in p.vertices)/len(p.vertices)>.35:p.material_index=len(actor.data.materials)-1
bodyshader=dissolve_shader(base_mat);handshader=dissolve_shader(handmat,True)
# A village set with two visibly different proxy actors.
V=Vector((30,-18,-.71));box('Village sand floor',V+Vector((0,0,-.04)),(15,16,.10),sand)
for x,y,w,d,h in [(-3,2,3.8,3.3,3.4),(3.4,3.7,3.6,4.0,3.7),(-4.8,7.0,4.0,4.2,3.3),(4.6,9.0,3.2,3.5,4.1)]:box('Village house',V+Vector((x,y,h/2)),(w,d,h),brick)
# Entrance at x=0, y=2: left/right wall pillars and overhead lintel, dark interior.
box('Doorway left',V+Vector((-1.0,2,1.35)),(.60,.8,2.7),brick)
box('Doorway right',V+Vector((1.0,2,1.35)),(.60,.8,2.7),brick)
box('Door lintel',V+Vector((0,2,2.55)),(2.7,.8,.5),brick)
box('Dark room',V+Vector((0,3.6,1.1)),(1.8,.3,2.2),black)
doorhinge=empty('Opening door hinge');doorhinge.location=V+Vector((-.69,1.73,0))
door=box('Timber door',(0,0,0),(1.32,.11,2.15),wood);door.parent=doorhinge;door.location=(.66,0,1.075)
light('Village warm doorway','AREA',V+Vector((0,1.3,3.0)),(1,.57,.25),180,2)
light('Village moon','AREA',V+Vector((-2,-4,5)),(.34,.52,1),650,5)
light('Village distant roofs','AREA',V+Vector((-1,8,9)),(1,.62,.31),1900,9)
window=material('Village window glow',(.8,.38,.08),0,.7,1.5)
for x,y in [(-3,3.67),(3.4,5.72),(-4.8,9.12),(4.6,10.77)]:
    box('Village window',V+Vector((x,y,1.5)),(.38,.025,.48),window,.01)
sphere('Sand beneath the fallen lamp',(.326,-2.445,.009),(.87,.66,.009),sand,32,12)
def villager(name,height,robe):
    o={};sc=height/1.65
    o['torso']=sphere(name+' tunic',(0,0,0),(.24*sc,.16*sc,.38*sc),robe)
    o['hips']=sphere(name+' lower robe',(0,0,0),(.23*sc,.16*sc,.18*sc),robe)
    o['head']=sphere(name+' head',(0,0,0),(.115*sc,.108*sc,.16*sc),skin)
    o['hair']=sphere(name+' hair',(0,0,0),(.12*sc,.113*sc,.09*sc),hair if height>1.5 else wood)
    for side in ['L','R']:
        o[side+'eye']=sphere(name+' eye',(0,0,0),(.011*sc,.007*sc,.012*sc),eyemat,12,6)
        for seg,r in [('upper',.065),('fore',.051),('thigh',.08),('shin',.06)]:o[side+seg]=sphere(name+' '+side+seg,(0,0,0),(r*sc,r*sc,.2*sc),robe if seg in ['upper','thigh'] else skin if seg=='fore' else wood)
        o[side+'hand']=sphere(name+' '+side+'hand',(0,0,0),(.047*sc,.038*sc,.065*sc),skin)
        o[side+'foot']=sphere(name+' '+side+'foot',(0,0,0),(.065*sc,.11*sc,.04*sc),wood)
    o['scale']=sc;return o
guardian=villager('Guardian',1.65,cloth);child=villager('Child',1.05,childcloth)
veins=[curve('Corrupted forearm vein',[(0,0,0)]*5,red,.008) for _ in range(3)]
def limb(obj,a,b,r,f):
    a=Vector(a);b=Vector(b);obj.location=(a+b)/2;obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();obj.scale=(r,r,(b-a).length/2+r*.2)
    for prop in ['location','rotation_euler','scale']:key(obj,prop,f)
def move_villager(o,origin,yaw,phase,walkweight,reach,f):
    sc=o['scale'];M=Matrix.Rotation(yaw,3,'Z');P=lambda x:Vector(origin)+M@(Vector(x)*sc)
    bob=.016*math.sin(phase*2)*walkweight
    for nm,local in [('torso',(0,0,.96+bob)),('hips',(0,0,.65+bob)),('head',(0,0,1.46+bob)),('hair',(0,.024,1.57+bob)),('Leye',(.041,-.103,1.49+bob)),('Reye',(-.041,-.103,1.49+bob))]:
        ob=o[nm];ob.location=P(local);ob.rotation_euler.z=yaw;key(ob,'location',f);key(ob,'rotation_euler',f)
    wrists=[]
    for side,sign in [('L',1),('R',-1)]:
        step=math.sin(phase+(0 if sign==1 else math.pi))*walkweight
        shoulder=P((sign*.225,0,1.16+bob));elbow=P((sign*.30,-.06-.12*step,.93+bob));wrist=P((sign*.29,-.12-.16*step,.75+bob))
        if reach is not None and side=='R':wrist=Vector(reach);elbow=(shoulder+wrist)/2+M@Vector((-.1,0,.03))
        limb(o[side+'upper'],shoulder,elbow,.061*sc,f);limb(o[side+'fore'],elbow,wrist,.045*sc,f)
        o[side+'hand'].location=wrist;key(o[side+'hand'],'location',f);wrists.append((elbow,wrist))
        hip=P((sign*.11,0,.65+bob));ankle=P((sign*.12,step*.16,.075+max(0,-step)*.10))
        knee=(hip+ankle)/2+M@Vector((0,-.065*sc,0));limb(o[side+'thigh'],hip,knee,.075*sc,f);limb(o[side+'shin'],knee,ankle,.05*sc,f)
        o[side+'foot'].location=ankle+M@Vector((0,-.035*sc,-.027*sc));o[side+'foot'].rotation_euler.z=yaw;key(o[side+'foot'],'location',f);key(o[side+'foot'],'rotation_euler',f)
    return wrists
# Stand-in current paths are animated in 3D, with points flowing into the lamp later.
C=Vector((-.65,5,2.1));streams=[]
currentbits=[]
for j in range(7):
    streams.append(curve('Magic current '+str(j),[(0,0,0)]*18,smoke if j%2 else red,.07 if j%2 else .027))
    currentbits.append([sphere('Current travelling light',(0,0,0),(.025,.025,.025),red,8,4) for _ in range(4)])
leak=curve('Lamp leak',[(0,0,0)]*10,red,.018)
particles=[]
for j in range(90):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1);o=bpy.context.object;o.name='Soul light '+str(j);o.data.materials.append(cyan)
    particles.append((o,Vector((random.uniform(-.32,.32),random.uniform(-.14,.14),random.uniform(.15,1.34))),random.random()))
fingerbits=[]
for j in range(12):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1);o=bpy.context.object;o.name='Fingertip light '+str(j);o.data.materials.append(cyan);fingerbits.append(o)
# Hairline cracks are attached to the crystal surface.
cracks=[]
for j in range(5):
    points=[(-.95+j*.17,4.59,1.50),(-.84+j*.13,4.60,1.88),(-.95+j*.14,4.72,2.15),(-.7+j*.09,4.82,2.61),(-.55+j*.035,4.94,3.11)]
    cracks.append(curve('Crystal fracture '+str(j),points,red,.016))
star_mat=material('Stars',(.52,.68,1),0,.5,2)
for i in range(70):
    a=random.uniform(0,math.tau);r=random.uniform(70,95);sphere('Star',(math.cos(a)*r,math.sin(a)*r,random.uniform(21,62)),(.055,.055,.055),star_mat,8,4)
# Each shot has a real camera. Markers switch cameras without interpolating across cuts.
cameras={}
for name,start,end,label in SHOTS:
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name+' camera',d);s.collection.objects.link(o);d.sensor_width=36;d.dof.use_dof=True;d.dof.aperture_fstop=5.6
    marker=s.timeline_markers.new(name+' '+label,frame=round(start*24)+1);marker.camera=o;cameras[name]=o
blackplane=box('Title darkness',(0,0,-10),(50,50,.2),black)
def camera(name,pos,target,lens,focus,f):
    o=cameras[name];o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();o.data.lens=lens
    o.data.dof.focus_distance=(Vector(focus)-o.location).length
    for p in ['location','rotation_euler']:key(o,p,f)
    for p in ['lens','dof.focus_distance']:key(o.data,p,f)
def set_curve(o,points,width,f):
    for p,co in zip(o.data.splines[0].points,points):p.co=(*co,1);p.keyframe_insert('co',frame=f)
    o.data.bevel_depth=max(.00001,width);key(o.data,'bevel_depth',f)
shotframes=[];diagnostics=[]
critical={1,1176}
for name,a,b,label in SHOTS:critical.update([round(a*24)+1,round(b*24)])
frames=sorted(set(range(1,1177,2))|critical)
for f in frames:
    s.frame_set(f);sec=(f-1)/24
    name,a,b,label=next(row for row in SHOTS if row[1]<=sec<row[2]);u=clamp((sec-a)/(b-a));k=ease(u)
    origin=Vector((0,-.50,0));yaw=0.;walkweight=0.;phase=sec*24;crouchweight=0.;raiseweight=1.;press=0.;release=0.
    if sec<4.5:
        w=ease(sec/2.2);origin.y=mix(.45,-.50,w);walkweight=1-ease((sec-1.6)/.6);raiseweight=0;yaw=-.35*ease((sec-3.3)/1.2)
    if 6<=sec<9:crouchweight=.28*math.sin(math.pi*ease((sec-6)/3));origin.y+=.12*math.sin(math.pi*u)
    if 12<=sec<15:press=ease((sec-12.6)/1.0)
    if 15<=sec<16.5:press=1
    if 16.5<=sec<19:press=1-ease((sec-16.5)/.75);release=ease((sec-16.5)/1.0)
    if 19<=sec<23:
        w=ease((sec-19)/2.6);origin=Vector((0,mix(-.50,-2.35,w),0));yaw=mix(0,1.05,ease((sec-20)/2.2));walkweight=.7*math.sin(math.pi*clamp((sec-19)/2.6));raiseweight=.8
    if sec>=23:origin=Vector((0,-2.35,0));yaw=1.05
    if sec>=28:press=ease((sec-28)/.55)
    if sec>=30.5:crouchweight=ease((sec-30.8)/4.0)
    rig.location=origin;rig.rotation_euler.z=yaw;rig.scale=(1.82,1.82,1.82)
    for prop in ['location','rotation_euler']:key(rig,prop,f)
    src=walk[int(phase)%36]
    for pb in arm.pose.bones:
        A=idle[pb.name].lerp(src[pb.name],walkweight);pb.matrix_basis=A.lerp(crouch[pb.name],crouchweight);pb.rotation_mode='QUATERNION'
        if pb.name=='mixamorig:Spine2':pb.rotation_quaternion=pb.rotation_quaternion@Quaternion((1,0,0),.015*math.sin(sec*3.2))
        if pb.name=='mixamorig:Head':
            nod=.2 if 12<=sec<19 else -.1 if sec>=19 else 0
            turn=-.5*ease((sec-3.0)/1.5) if sec<4.5 else -.25 if 15<=sec<19 else .16 if sec>=19 else 0
            pb.rotation_quaternion=pb.rotation_quaternion@Quaternion((1,0,0),nod)@Quaternion((0,1,0),turn)
        if 'Hand' in pb.name and any(x in pb.name for x in ['Index','Middle','Ring','Pinky']) and not pb.name.endswith('4'):
            pb.rotation_quaternion=Quaternion((1,0,0),.50 if 'Left' in pb.name else .12)
        for prop in ['location','rotation_quaternion','scale']:key(pb,prop,f)
    lampheight=mix(1.14,.65,crouchweight);lamplocal=Vector((.08,-.33, lampheight));badgeLocal=lamplocal+Vector((-.09,-.10,-.087))
    for side,(target,pole,rot,ik,co) in hands.items():
        sign=1 if side=='Left' else -1
        carry=sec<4.5
        ik.influence=0 if carry else 1;co.influence=0 if carry else 1;key(ik,'influence',f);key(co,'influence',f)
        if side=='Left':pt=Vector((.13,-.32,lampheight+.025));direction=Vector((-.05,-.04,-1))
        else:
            hover=Vector((-.28,-.28,lampheight+.22));contact=badgeLocal+Vector((-.04,.10,.11));pt=hover.lerp(contact,press)
            pt=pt.lerp(Vector((-.42,-.28,lampheight+.20)),release);direction=Vector((.15,-.55,-.83))
        target.location=local_to_world(pt);pole.location=local_to_world((sign*.8,-.08,mix(1.1,.58,crouchweight)))
        rot.rotation_euler=worlddir(direction).to_track_quat('Y','Z').to_euler()
        for ob in [target,pole]:key(ob,'location',f)
        key(rot,'rotation_euler',f)
    bpy.context.view_layer.update()
    if press>0:
        target=hands['Right'][0]
        fingertip=arm.matrix_world@arm.pose.bones['mixamorig:RightHandIndex3'].tail
        target.location+=(local_to_world(badgeLocal)-fingertip)*press
        key(target,'location',f);bpy.context.view_layer.update()
    if sec<4.5:
        hand=(arm.matrix_world@arm.pose.bones['mixamorig:LeftHand'].matrix).translation
        lamp.location=hand+Vector((.04,0,0));lamp.rotation_euler=(0,0,yaw)
    else:lamp.location=local_to_world(lamplocal);lamp.rotation_euler=(0,0,math.pi+yaw)
    if sec>=39:
        dt=sec-39;lamp.location=local_to_world((.08,-.33,.65));lamp.location.z=max(.184,.65-4.905*dt*dt)
        if dt>.31:lamp.location.z=.184+max(0,.06*math.sin((dt-.31)*13))*math.exp(-(dt-.31)*5)
        lamp.rotation_euler.x=.17*math.sin(max(0,dt-.3)*17)*math.exp(-max(0,dt-.3)*3)
    for prop in ['location','rotation_euler']:key(lamp,prop,f)
    bpy.context.view_layer.update();seal=lamp.matrix_world@Vector((.09,.10,-.087))
    dissolve=-1 if sec<33 else mix(.08,.64,ease((sec-33)/3.5)) if sec<36.5 else mix(.64,1.45,ease((sec-38.05)/.95))
    glow=ease((press-.90)/.10)*.82 if 12<=sec<19 or 28<=sec<39 else 0
    for shader in [bodyshader,handshader]:shader[0].outputs[0].default_value=dissolve;shader[0].outputs[0].keyframe_insert('default_value',frame=f)
    handshader[1].outputs[0].default_value=glow;handshader[1].outputs[0].keyframe_insert('default_value',frame=f)
    sealglow.location=seal+worlddir((0,-.05,.08));key(sealglow,'location',f)
    sealglow.data.energy=(.2+2.5*press) if 12<=sec<39 else 3*math.sin(math.pi*clamp((sec-45.65)/1.15)) if 45.5<=sec<47 else .2;key(sealglow.data,'energy',f)
    badge.scale=(1,1,1);badge.hide_render=sec<12;key(badge,'hide_render',f)
    cy=cyan.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'];cy.default_value=.4+1.2*glow if sec<39 else .4+1.5*math.sin(math.pi*clamp((sec-45.65)/1.15));cy.keyframe_insert('default_value',frame=f)
    # Leak stops on contact, returns when the hand is withdrawn.
    leak_on=(1-press) if 12<=sec<19 else .8 if 4.5<=sec<12 else 0
    spout=lamp.matrix_world@Vector((.32,0,-.035));pts=[spout+Vector((math.sin(sec*4+i*.8)*.04,0,i*.045)) for i in range(10)]
    set_curve(leak,pts,.018*leak_on,f)
    for j,ob in enumerate(fingerbits):
        t=(sec*1.7+j/12)%1;start=(arm.matrix_world@arm.pose.bones['mixamorig:RightHandIndex3'].matrix).translation
        ob.location=start.lerp(seal,t)+Vector((math.sin(t*math.tau+j)*.02,0,.05*math.sin(t*math.pi)));r=.010*glow*math.sin(math.pi*t);ob.scale=(r,r,r)
        key(ob,'location',f);key(ob,'scale',f)
    for j,ob in enumerate(cracks):ob.data.bevel_depth=.00001 if sec<4.5 or sec>=39 else .013*ease((sec-4.5)/1.5);key(ob.data,'bevel_depth',f)
    active=6<=sec<39;backflow=sec>=30.5
    current_weight=(1-.65*press) if 12<=sec<19 else 1
    for j,ob in enumerate(streams):
        ob.hide_render=not active;key(ob,'hide_render',f)
        angle=j*math.tau/7;destination=Vector((math.cos(angle)*30+12,math.sin(angle)*24-9,.4))
        points=[]
        for i in range(18):
            q=i/17
            if backflow:
                p=destination.lerp(seal,q);p.z+=math.sin(q*math.pi)*(1.6+j*.25);p+=Vector((math.sin(q*9-sec*3+j),math.cos(q*8-sec*3+j),0))*.25*math.sin(q*math.pi)
            else:
                extent=ease((sec-6)/2.2);p=C.lerp(destination,q*extent);p.z+=math.sin(q*math.pi)*(.6+j*.13)+.13*math.sin(sec*5+q*11+j)
            points.append(p)
        fade=1-ease((sec-37.5)/1.5) if backflow else 1
        set_curve(ob,points,(.022 if j%2 else .009)*current_weight*fade if active else .00001,f)
        for qbit,bit in enumerate(currentbits[j]):
            q=(sec*.65+qbit/4+j*.11)%1;ix=min(16,int(q*17));bit.location=points[ix].lerp(points[ix+1],q*17-ix)
            radius=.045*current_weight*fade*math.sin(q*math.pi) if active else .00001
            bit.scale=(radius,radius,radius);key(bit,'location',f);key(bit,'scale',f)
    for j,(ob,source,seed) in enumerate(particles):
        t=(sec*1.2+seed)%1;start=local_to_world(source);p=start.lerp(seal,t)+Vector((math.sin(t*math.tau+seed*7),math.cos(t*math.tau+seed*7),0))*.14*math.sin(math.pi*t)
        on=33<=sec<39 and source.z<dissolve+.20 and source.z>dissolve-.4
        ob.location=p;r=(.009+.012*seed)*math.sin(math.pi*t) if on else .00001;ob.scale=(r,r,r);key(ob,'location',f);key(ob,'scale',f)
    # The two village shots use the same actors, doorway and opposite outcome.
    if sec<42:
        move=ease((sec-9.2)/2.2);gpos=V+Vector((-.25,mix(-.8,2.0,move),0));cpos=V+Vector((.22,mix(-.5,2.7,move),0));vyaw=math.pi
        doorangle=math.radians(-100);walking=math.sin(math.pi*move)
    else:
        move=ease((sec-42.2)/2.8);gpos=V+Vector((-.3,mix(2.0,.5,move),0));cpos=V+Vector((.28,mix(2.7,-.45,move),0));vyaw=0;doorangle=math.radians(-100*ease((sec-42)/.8));walking=math.sin(math.pi*move)
    doorhinge.rotation_euler.z=doorangle;key(doorhinge,'rotation_euler',f)
    wrists=move_villager(guardian,gpos,vyaw,sec*6,walking,cpos+Vector((-.12,0,.76)),f)
    move_villager(child,cpos,vyaw,sec*7,walking,gpos+Vector((.13,0,.75)),f)
    elbow,wrist=wrists[1]
    for j,ob in enumerate(veins):
        pts=[elbow.lerp(wrist,i/4)+Vector((.045+.012*j,math.sin(i*1.7+j)*.015,0)) for i in range(5)];set_curve(ob,pts,.009 if 9<=sec<12 else .00001,f)
    head=(arm.matrix_world@arm.pose.bones['mixamorig:Head'].matrix).translation+Vector((0,0,.17));body=origin+Vector((0,0,mix(1,.60,crouchweight)))
    # Camera placement is authored in metres, not generated from stills.
    if name=='R01':
        angle=math.radians(48-100*k);reveal=ease((u-.48)/.52)
        pos=Vector((math.cos(angle)*4.65,math.sin(angle)*4.65+.25,1.12+.68*k))
        target=Vector((-.3*reveal,origin.y+1.45*reveal,1.08+.9*reveal));camera(name,pos,target,35,body.lerp(C,ease((reveal-.5)/.5)),f)
    elif name=='R02':camera(name,(-1.2,2.0,2.45),(-.70,4.66,2.24),85,(-.70,4.66,2.24),f)
    elif name=='R03':
        pos=vmix((-15,-24,13),(-18,-27,15),k);camera(name,pos,(8,-3,1.2),25,C,f)
    elif name=='R04':
        pos=V+vmix((2.5,-3.0,1.3),(2.6,-1.5,1.35),k);target=(gpos+cpos)/2+Vector((0,0,.9));camera(name,pos,target,40,target,f)
    elif name in ['R05','R10']:
        offset=Vector((mix(-.65,-.83,k),-1.54,1.31));pos=local_to_world(offset);camera(name,pos,seal+Vector((0,0,.055)),70,seal,f)
    elif name=='R06':camera(name,local_to_world((-.75,-1.85,1.73)),head-Vector((0,0,.10)),70,head,f)
    elif name=='R07':
        pos=local_to_world((1.85,-3.1,1.52));target=origin+Vector((-.30,.8,1.3));focus=head.lerp(C,ease((u-.45)/.55));camera(name,pos,target,46,focus,f)
    elif name=='R08':
        pos=origin+vmix((2.8,1.5,1.7),(-2.15,1.30,3.1),k);target=(origin+Vector((0,0,1.3))).lerp(Vector((18,-11,.6)),ease((u-.48)/.52));camera(name,pos,target,35,head.lerp(V,ease((u-.65)/.35)),f)
    elif name=='R09':camera(name,local_to_world((.64,-1.68,1.74)),head+worlddir((-.025,0,-.13)),65,head,f)
    elif name=='R11':
        pos=local_to_world((mix(2.5,.7,k),-3.4,1.20));target=origin+Vector((0,0,.95));camera(name,pos,target,35,body,f)
    elif name=='R12':camera(name,local_to_world((.56,-1.85,1.08)),head-Vector((0,0,.16)),70,head,f)
    elif name=='R13':
        pos=local_to_world((.70,-1.70,.43));target=lamp.location+Vector((-.07,0,-.07));camera(name,pos,target,50,target,f)
    elif name=='R14':
        pos=V+Vector((2.5,-3.0,1.3));target=V+Vector((0,.9,.92));camera(name,pos,target,45,target,f)
    elif name=='R15':
        pos=local_to_world((.54,-1.70,.40));target=lamp.location+Vector((-.04,0,-.075));camera(name,pos,target,85,target,f)
    else:camera(name,(0,0,-8),(0,0,-10),35,(0,0,-10),f)
    if f in [round(a*24)+1,round((a+b)/2*24)+1,round(b*24)]:diagnostics.append(dict(frame=f,shot=name,camera=list(cameras[name].location),lamp=list(lamp.location),seal=list(seal),head=list(head)))
    if f%120==1:print('Baked',f,'of 1176',flush=True)
# Linear interpolation inside shots; boolean visibility remains discrete.
for action in bpy.data.actions:
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag=strip.channelbag(slot,ensure=False)
                if bag:
                    for fc in bag.fcurves:
                        for p in fc.keyframe_points:p.interpolation='CONSTANT' if 'hide_' in fc.data_path else 'LINEAR'
s.frame_set(1);s.camera=cameras['R01']
s.render.image_settings.file_format='PNG';s.render.filepath=str(OUT/'frames/f_')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SandGuard-Ending49-Previs.blend'))
(OUT/'shot-plan.json').write_text(json.dumps([dict(id=n,start=a,end=b,label=l,startFrame=round(a*24)+1,endFrame=round(b*24)) for n,a,b,l in SHOTS],ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'blocking-checks.json').write_text(json.dumps(diagnostics,indent=2),encoding='utf-8')
if '--preview' in sys.argv:
    for name,a,b,label in SHOTS[:-1]:
        f=round((a+b)/2*24)+1;s.frame_set(f);s.camera=cameras[name];s.render.filepath=str(OUT/'qa'/f'{name}-mid.png');bpy.ops.render.render(write_still=True)
if '--render' in sys.argv:
    (OUT/'frames').mkdir(exist_ok=True);s.frame_set(1);s.camera=cameras['R01'];s.render.filepath=str(OUT/'frames/f_');bpy.ops.render.render(animation=True)
