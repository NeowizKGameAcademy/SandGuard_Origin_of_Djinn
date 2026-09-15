"""Editable Blender source and FBX exports for four temple environment props."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/course/unity/SandGuard')
OUT=ROOT/'Assets/TempleArt/Models'
SOURCE=ROOT/'Docs/model-art/temple-heroes-v1'
OUT.mkdir(parents=True,exist_ok=True); SOURCE.mkdir(parents=True,exist_ok=True)
random.seed(47)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)

def material(name,color,metal=0,rough=.8,emission=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    return m
stone=material('Temple_Sandstone',(.57,.40,.22))
light=material('Temple_CutStone',(.72,.55,.32))
dark=material('Temple_ShadowStone',(.30,.205,.11))
bronze=material('Temple_OldBronze',(.35,.235,.095),.65,.58)
gold=material('Temple_WornGold',(.62,.41,.15),.65,.5)
teal=material('Temple_Turquoise',(.075,.30,.28),.15,.65)
glow=material('Temple_SealGlow',(.12,.72,.65),.1,.45,.6)
materials=[stone,light,dark,bronze,gold,teal,glow]
current=[];groups={};reports={}

def finish(obj,name,mat,bevel=0):
    obj.name=name;obj.data.materials.append(mat)
    if bevel:
        mod=obj.modifiers.new('Worn edge bevel','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
    current.append(obj);return obj

def box(name,loc,scale,mat=stone,bevel=.04):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat,bevel)

def mesh(name,verts,faces,mat=stone):
    d=bpy.data.meshes.new(name);d.from_pydata(verts,[],faces);d.update()
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o)
    return finish(o,name,mat)

def cylinder(name,loc,radius,depth,mat=stone,vertices=16,rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    o=bpy.context.object
    if rotation:o.rotation_euler=rotation
    return finish(o,name,mat,.018)

def ico(name,loc,scale,mat=stone,subdiv=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv,radius=1,location=loc)
    o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat)

def line(name,points,radius=.02,mat=gold):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=1
    curve.bevel_depth=radius;curve.bevel_resolution=1;curve.resolution_u=1
    spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
    for p,co in zip(spline.points,points):p.co=(*co,1)
    o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,name,mat)

def radial_profile(name,center,profile,mat=stone,n=24):
    verts=[];faces=[]
    for radius,z in profile:
        for a in range(n):
            angle=2*math.pi*a/n;verts.append((center[0]+radius*math.cos(angle),center[1]+radius*math.sin(angle),center[2]+z))
    for j in range(len(profile)-1):
        for a in range(n):b=j*n+a;c=j*n+(a+1)%n;faces.append((b,c,c+n,b+n))
    faces.extend([tuple(reversed(range(n))),tuple((len(profile)-1)*n+a for a in range(n))])
    return mesh(name,verts,faces,mat)

def chips(radius,count,z=0):
    for i in range(count):
        a=random.uniform(0,6.283);r=random.uniform(radius*.75,radius)
        size=random.uniform(.06,.18)
        o=ico('Loose sandstone fragment',(math.cos(a)*r,math.sin(a)*r,z+size*.4),(size,size*.8,size*.5),light,1)
        o.rotation_euler.z=random.uniform(0,6.28)

def seal():
    box('Lower plinth',(0,0,.12),(1.65,1.35,.24),stone,.09)
    box('Upper plinth',(0,0,.31),(1.37,1.10,.18),light,.045)
    verts=[]
    # Chamfered octagonal cross sections, narrowing to a wind-worn asymmetric apex.
    cross=[(-.5,-.4),(.5,-.4),(.62,-.27),(.62,.27),(.5,.4),(-.5,.4),(-.62,.27),(-.62,-.27)]
    for z,s,dx in [(.40,1,0),(1.0,.97,0),(2.25,.75,-.015),(2.94,.52,.10),(3.25,.15,.02)]:
        for x,y in cross:verts.append((x*s+dx,y*s,z))
    faces=[tuple(reversed(range(8)))]
    for j in range(4):
        for a in range(8):faces.append((j*8+a,j*8+(a+1)%8,(j+1)*8+(a+1)%8,(j+1)*8+a))
    faces.append(tuple(range(32,40)));mesh('Faceted wind-worn monolith',verts,faces,stone)
    for z,w in [(1.05,1.14),(1.26,1.10)]:box('Bronze retaining band',(0,0,z),(w,.83,.085),bronze,.02)
    cylinder('Seal medallion',(0,-.437,1.16),.23,.065,bronze,24,(math.pi/2,0,0))
    cylinder('Turquoise seal heart',(0,-.48,1.16),.115,.025,teal,16,(math.pi/2,0,0))
    line('Luminous fractured seam',[(0,-.416,.43),(.025,-.426,.77),(-.02,-.437,1.0),(0,-.40,1.46),(.04,-.35,1.9),(-.01,-.31,2.23),(.07,-.26,2.68),(.06,-.2,2.91)],.022,glow)
    for sign in [-1,1]:
        for j in range(3):
            points=[]
            for k in range(18):
                t=k/17;z=1.52+j*.31+t*.29;x=sign*(.09+.27*math.sin(t*math.pi))
                y=-.40+(z-1)*.072-.012;points.append((x,y,z))
            line('Wind curl relief',points,.018,gold)
    chips(.95,5)

def broken_shaft(name,center,height,r=.48):
    n=32;verts=[];faces=[]
    tops=[height+random.uniform(-.19,.17) for i in range(n)]
    for j,z in enumerate([0,.15,height*.45,height]):
        for a in range(n):
            angle=2*math.pi*a/n;rr=r*(1 if a%2==0 else .95)*(1-.06*j/3)
            verts.append((center[0]+rr*math.cos(angle),center[1]+rr*math.sin(angle),center[2]+(tops[a] if j==3 else z)))
    for j in range(3):
        for a in range(n):faces.append((j*n+a,j*n+(a+1)%n,(j+1)*n+(a+1)%n,(j+1)*n+a))
    verts.append((center[0]+.08,center[1]-.05,center[2]+height-.12));cap=len(verts)-1
    for a in range(n):faces.append((3*n+a,3*n+(a+1)%n,cap))
    faces.append(tuple(reversed(range(n))))
    o=mesh(name,verts,faces,stone);o.data.materials.append(light)
    for p in list(o.data.polygons)[3*n:]:p.material_index=1
    return o

def columns():
    box('Column base slab',(-.5,0,.10),(1.35,1.35,.2),stone,.07)
    radial_profile('Column base molding',(-.5,0,.2),[(.62,0),(.62,.10),(.51,.17),(.51,.22)],light)
    broken_shaft('Standing broken fluted column',(-.5,0,.42),2.35)
    for z in [.64,.80]:radial_profile('Painted surviving collar',(-.5,0,z),[(.485,0),(.485,.07)],teal)
    radial_profile('Bronze thin rim',(-.5,0,.74),[(.491,0),(.491,.025)],bronze)
    box('Second stump foundation',(.95,.4,.08),(1,1,.16),stone,.06)
    broken_shaft('Low fractured stump',(.95,.4,.16),.65,.36)
    fallen=broken_shaft('Fallen shaft',(0,0,0),1.2,.42)
    fallen.rotation_euler=(math.pi/2,.1,-.5);fallen.location=(.4,-.8,.48)
    radial_profile('Fallen lotus capital',(1.23,-.5,.07),[(.48,0),(.51,.15),(.43,.32),(.25,.43)],light,n=12)
    for a in range(8):
        t=a*math.pi/4
        line('Capital petal rim',[(1.23+.44*math.cos(t),-.5+.44*math.sin(t),.14),(1.23+.35*math.cos(t),-.5+.35*math.sin(t),.31),(1.23+.25*math.cos(t),-.5+.25*math.sin(t),.44)],.025,teal)
    chips(1.7,11)

def altar():
    box('Stepped altar base',(0,0,.12),(2.8,2.5,.24),stone,.08)
    box('Altar shadow course',(0,0,.31),(2.51,2.2,.15),dark,.035)
    box('Altar body',(0,0,.65),(2.36,2.06,.57),stone,.055)
    box('Overhanging altar cap',(0,0,1.02),(2.68,2.38,.2),light,.07)
    box('Recessed offering bed',(0,0,1.126),(1.67,1.37,.015),dark,.02)
    for x in [-1.03,1.03]:
        for y in [-.9,.9]:
            box('Corner pier',(x,y,.78),(.35,.35,1.03),stone,.04)
            box('Pier bronze cap',(x,y,1.29),(.39,.39,.08),bronze,.03)
            box('Pier faience insert',(x,y-.182,.89),(.17,.018,.39),teal,.012)
    radial_profile('Offering basin',(0,0,1.14),[(.59,0),(.64,.09),(.58,.19),(.44,.18),(.39,.045)],bronze,32)
    cylinder('Quiet seal inset',(0,0,1.185),.31,.02,teal,24)
    for sign in [-1,1]:
        line('Gold channel',[(sign*.57,0,1.145),(sign*.85,0,1.145),(sign*1.0,-.64,1.145)],.022,gold)
        line('Axial offering line',[(0,sign*.60,1.145),(0,sign*1.04,1.145)],.022,gold)
    for i in range(5):
        x=(i-2)*.32
        box('Facade inset',(x,-1.04,.65),(.16,.02,.22),teal,.01)
    line('Wing motif left',[(-.05,-1.065,.57),(-.3,-1.065,.71),(-.69,-1.065,.76),(-.87,-1.065,.91)],.025,gold)
    line('Wing motif right',[(.05,-1.065,.57),(.3,-1.065,.71),(.69,-1.065,.76),(.87,-1.065,.91)],.025,gold)

def guardian():
    box('Statue lower plinth',(0,.16,.13),(2.50,4.30,.26),stone,.10)
    box('Statue upper course',(0,.16,.32),(2.29,4.06,.15),light,.05)
    ico('Recumbent guardian body',(0,.50,.98),(.88,1.40,.66),stone,2)
    ico('Raised chest',(0,-.48,1.34),(.70,.68,.85),stone,2)
    for x in [-.5,.5]:
        box('Long foreleg',(x,-1.02,.69),(.50,1.62,.45),stone,.15)
        box('Rounded stone paw',(x,-1.78,.63),(.55,.59,.40),light,.12)
        for d in [-.12,.12]:line('Paw incision',[(x+d,-2.03,.64),(x+d,-1.84,.78)],.016,dark)
        ico('Folded hind haunch',(x*.95,1.33,.78),(.47,.60,.43),stone,2)
    ico('Tall jackal neck',(0,-.49,1.91),(.48,.51,.74),stone,2)
    ico('Angular jackal skull',(0,-.62,2.38),(.57,.61,.53),stone,2)
    # Long tapered canine muzzle with planar cheeks.
    verts=[]
    for y,z,w,h in [(-.83,2.25,.43,.28),(-1.35,2.18,.29,.20),(-1.70,2.15,.15,.13)]:
        verts.extend([(-w,y,z-h),(w,y,z-h),(w,y,z+h),(-w,y,z+h)])
    faces=[(3,2,1,0)]
    for j in range(2):
        for k in range(4):faces.append((j*4+k,j*4+(k+1)%4,(j+1)*4+(k+1)%4,(j+1)*4+k))
    faces.append((8,9,10,11));mesh('Tapered jackal muzzle',verts,faces,stone)
    ico('Worn nose tip',(0,-1.71,2.16),(.16,.10,.13),dark,1)
    for sign in [-1,1]:
        x=sign*.35;height=3.65 if sign>0 else 3.45
        verts=[(x-.22,-.47,2.63),(x+.22,-.47,2.63),(x+sign*.12,-.31,height),(x-.19,-.12,2.62),(x+.19,-.12,2.62),(x+sign*.12,-.10,height-.04)]
        mesh('Tall triangular jackal ear',verts,[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],stone)
        mesh('Recessed ear bronze',[(x-.115,-.478,2.76),(x+.115,-.478,2.76),(x+sign*.10,-.321,height-.23)],[(0,1,2)],bronze)
        line('Carved almond eye',[(sign*.37,-1.045,2.44),(sign*.46,-.96,2.49),(sign*.51,-.85,2.47)],.032,dark)
        line('Cheek inlay',[(sign*.42,-.98,2.38),(sign*.48,-.72,2.20),(sign*.41,-.58,2.06)],.022,bronze)
    # Broad ceremonial collar, fan-shaped and divided into readable panels.
    for i in range(9):
        a=-1.20+i*.30
        x=math.sin(a)*.66;z=1.37+abs(math.sin(a))*.36;y=-1.035+.17*abs(math.sin(a))
        o=box('Faience collar panel',(x,y,z),(.18,.06,.37),teal,.015);o.rotation_euler.y=-a*.65
    line('Collar upper bronze edge',[(math.sin(-1.25+i/16*2.5)*.69,-1.055+.17*abs(math.sin(-1.25+i/16*2.5)),1.57+abs(math.sin(-1.25+i/16*2.5))*.34) for i in range(17)],.03,gold)
    line('Curled stone tail',[(.6,1.5,.8),(.9,1.40,.64),(1.01,1.01,.58),(.96,.64,.55),(.80,.56,.56)],.09,stone)
    line('Old chest crack',[(-.18,-1.10,1.98),(-.12,-1.145,1.86),(-.21,-1.14,1.71)],.016,dark)

builders=[('SealStone',seal),('BrokenColumns',columns),('OfferingAltar',altar),('JackalGuardian',guardian)]
for name,fn in builders:
    current=[];fn()
    bpy.ops.object.select_all(action='DESELECT')
    for o in current:o.select_set(True)
    bpy.context.view_layer.objects.active=current[0];bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Flat faces on carved stone are intentional; bevel geometry softens hard edges.
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1)
    obj.data.calc_loop_triangles()
    reports[name]={'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),'dimensions_m':list(obj.dimensions),'uv_layers':len(obj.data.uv_layers)}
    groups[name]=obj
    obj.hide_set(True)

# Editable .blend keeps separate named complete hero meshes, arranged for inspection.
for i,(name,obj) in enumerate(groups.items()):
    obj.hide_set(False);obj.location=((i%2)*5,(i//2)*6,0)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Temple_Hero_Props_v1.blend'))
(SOURCE/'mesh-report.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('TEMPLE_HERO_EXPORT_COMPLETE',json.dumps(reports))
