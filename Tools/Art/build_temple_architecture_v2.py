"""Blender-built courtyard temple. Unity gameplay decks/treads are reference-only.
Run with Blender --background --python Tools/Art/build_temple_architecture_v2.py.
"""
import bpy, bmesh, json, math, random
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/TempleArt/ArchitectureV2'
DOC=ROOT/'Docs/LevelArt/TempleArchitecture/V2'
OUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
parts=json.loads((ROOT/'Docs/LevelArt/TempleArchitecture/structure-reference.json').read_text())['parts']
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(9017)
palette={'Temple_Sandstone':(.70,.595,.445),'Temple_Limestone':(.82,.74,.59),'Temple_Recess':(.39,.30,.20),'Temple_Paving':(.68,.57,.42),
         'Temple_Turquoise':(.10,.31,.28),'Temple_Bronze':(.39,.27,.13),'Temple_Sand':(.64,.48,.29)}
mats={}
for name,col in palette.items():
    m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);mats[name]=m
def xyz(v):return (v['x'],v['y'],v['z'])
def coord(v):return (v[0],-v[2],v[1])
pv=[];pf=[]
for part in parts:
    offset=len(pv);pv.extend(map(xyz,part['vertices']))
    for j in range(0,len(part['triangles']),3):
        f=tuple(offset+i for i in part['triangles'][j:j+3]);a,b,c=[Vector(pv[i]) for i in f]
        if (b-a).cross(c-a).y>1e-6:pf.append(f)
protect=BVHTree.FromPolygons(pv,pf,all_triangles=True)
groups={};region='D_Detail';skipped=0
relief_placements=[]
imported_regions=set()
custom_motifs={}
monumental_reliefs=[]
stair_closures=0
def clear(vs):
    global skipped
    lo=[min(v[i] for v in vs) for i in range(3)];hi=[max(v[i] for v in vs) for i in range(3)]
    nx=max(1,math.ceil((hi[0]-lo[0])/.8));nz=max(1,math.ceil((hi[2]-lo[2])/.8))
    for a in range(nx+1):
        for b in range(nz+1):
            x=lo[0]+(hi[0]-lo[0])*a/nx;z=lo[2]+(hi[2]-lo[2])*b/nz
            hit=protect.ray_cast(Vector((x,hi[1]+3,z)),Vector((0,-1,0)),hi[1]-lo[1]+6)[0]
            if hit is not None and hi[1]>hit.y+.012 and lo[1]<hit.y+2.5:
                skipped+=1;return False
    return True
def add(vs,fs,mat='Temple_Sandstone',check=False):
    if check and not clear(vs):return False
    vertices,faces=groups.setdefault((region,mat),([],[]));offset=len(vertices)
    vertices.extend(map(coord,vs));faces.extend(tuple(offset+i for i in f) for f in fs);return True
BOXF=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
def box(c,size,mat='Temple_Limestone',yaw=0,check=None):
    if check is None:check=region.startswith('D_')
    x,y,z=c;w,h,d=[s/2 for s in size];cs,sn=math.cos(yaw),math.sin(yaw)
    vs=[(x+u*cs-v*sn,y+dy,z+u*sn+v*cs) for u,dy,v in [(-w,-h,-d),(w,-h,-d),(w,h,-d),(-w,h,-d),(-w,-h,d),(w,-h,d),(w,h,d),(-w,h,d)]]
    return add(vs,BOXF,mat,check)
def hull(points):
    p=sorted(set((round(v[0],3),round(v[2],3)) for v in points))
    def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    a=[];b=[]
    for q in p:
        while len(a)>1 and cross(a[-2],a[-1],q)<=.0001:a.pop()
        a.append(q)
    for q in p[::-1]:
        while len(b)>1 and cross(b[-2],b[-1],q)<=.0001:b.pop()
        b.append(q)
    return a[:-1]+b[:-1]
def prism(poly,bottom,top,mat='Temple_Sandstone',scale_bottom=1,scale_top=1):
    cx=sum(x for x,z in poly)/len(poly);cz=sum(z for x,z in poly)/len(poly);n=len(poly)
    vs=[(cx+(x-cx)*s,y,cz+(z-cz)*s) for y,s in [(bottom,scale_bottom),(top,scale_top)] for x,z in poly]
    fs=[tuple(range(n)),tuple(range(n*2-1,n-1,-1))]
    fs.extend((i,i+n,(i+1)%n+n,(i+1)%n) for i in range(n));add(vs,fs,mat)
def local(cx,cz,tx,tz,nx,nz,s,y,out):return (cx+tx*s+nx*out,y,cz+tz*s+nz*out)
def strip(points,width,depth,mat='Temple_Limestone'):
    # Extruded continuous lines, used for carved sun/ribbon/feather relief.
    vs=[];fs=[]
    for j,(x,y,z) in enumerate(points):
        a=Vector(points[max(0,j-1)]);b=Vector(points[min(len(points)-1,j+1)]);t=(b-a).normalized()
        side=Vector((-t.y,t.x,0)) if abs(t.z)<.01 else Vector((0,-t.z,t.y))
        if side.length<.01:side=Vector((1,0,0))
        side.normalize()
        for dd in [-depth/2,depth/2]:
            for sign in [-1,1]:vs.append((x+side.x*width*sign/2,y+side.y*width*sign/2,z+side.z*width*sign/2+dd))
        if j:
            k=j*4;f=k-4;fs.extend([(f,f+1,k+1,k),(f+2,k+2,k+3,f+3),(f,k,k+2,f+2),(f+1,f+3,k+3,k+1)])
    fs.extend([(0,2,3,1),(len(vs)-4,len(vs)-3,len(vs)-1,len(vs)-2)]);add(vs,fs,mat,True)
def wall_relief(cx,cz,tx,tz,nx,nz,base,height,width=2.5,out=0,slope=0,design_override=None):
    global region
    # Towers have real columns and inset cells. The old detached tablets are removed.
    if region.startswith('D_Pylon'):return
    original_region=region
    imported_kind={'D_Wall_0':'wall-deco','D_Wall_1':'wall-deco-anubis','D_Wall_2':'god-la'}.get(region)
    if imported_kind and region not in imported_regions:
        imported_regions.add(region)
        ph=min(height,width*1.05)
        relief_placements.append(dict(kind=imported_kind,cx=cx,cz=cz,tx=tx,tz=tz,nx=nx,nz=nz,
            base=base+(height-ph)/2,height=ph,width=width,out=out,slope=slope))
        return
    designs=['lotus','winged_sun','scarab','watchful_eye','papyrus','constellation','stepped_diamond','river']
    serial=sum(custom_motifs.values());design=design_override or designs[serial%len(designs)]
    custom_motifs[design]=custom_motifs.get(design,0)+1
    region='D_CustomMotif_'+str(serial)+'_'+design
    h=min(height,width*(1.8 if design_override else (1.05 if serial%3==0 else 1.55)))
    base+=(height-h)/2;height=h
    def point(u,v,depth):
        return local(cx,cz,tx,tz,nx,nz,u*width,base+v*height,out+depth-slope*v*height)
    def line(points,w=.035,mat='Temple_Recess'):
        # Raised carved edges are rooted inside the real supporting wall; no tablet backing.
        w*=max(1,min(2.6,width/2.4))
        vv=[];ff=[]
        for j,(u,v) in enumerate(points):
            a=points[max(0,j-1)];b=points[min(len(points)-1,j+1)]
            dx,dy=(b[0]-a[0])*width,(b[1]-a[1])*height
            length=max(.00001,math.hypot(dx,dy))
            for depth in [-.055,.205]:
                for sign in [-1,1]:
                    vv.append(point(u+sign*dy/length*w/width/2,v-sign*dx/length*w/height/2,depth))
            if j:
                k=j*4;q=k-4
                ff.extend([(q,q+1,k+1,k),(q+2,k+2,k+3,q+3),(q,k,k+2,q+2),(q+1,q+3,k+3,k+1)])
        ff.extend([(0,2,3,1),(len(vv)-4,len(vv)-3,len(vv)-1,len(vv)-2)])
        add(vv,ff,mat,True)
    def oval(u,v,rx,ry,mat='Temple_Recess'):
        line([(u+rx*math.cos(j*math.tau/32),v+ry*math.sin(j*math.tau/32)) for j in range(33)],.045,mat)
    def filled(points,mat='Temple_Limestone',depth=.09):
        # Closed extruded polygon: broad carved surfaces instead of outline-only symbols.
        n=len(points);vs=[point(u,v,d) for d in [-.065,depth] for u,v in points]
        fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
        fs.extend((j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n))
        add(vs,fs,mat,True)
    def disk(u,v,rx,ry,mat='Temple_Turquoise',depth=.10):
        filled([(u+rx*math.cos(j*math.tau/32),v+ry*math.sin(j*math.tau/32)) for j in range(32)],mat,depth)
    accent='Temple_Turquoise' if serial%2 else 'Temple_Bronze'
    if design=='lotus':
        for sign in [-1,1]:
            filled([(0,.19),(sign*.29,.35),(sign*.39,.62),(sign*.16,.5)],'Temple_Limestone')
            filled([(sign*.06,.28),(sign*.25,.39),(sign*.32,.55),(sign*.16,.47)],'Temple_Turquoise',.12)
        filled([(0,.19),(-.12,.55),(0,.84),(.12,.55)],'Temple_Turquoise',.12)
        filled([(-.36,.11),(.36,.11),(.32,.18),(-.32,.18)],'Temple_Bronze',.12)
    elif design=='winged_sun':
        disk(0,.65,.135,.13,'Temple_Bronze',.17)
        for sign in [-1,1]:
            for j in range(5):
                u=sign*(.16+j*.048)
                filled([(u,.66),(u+sign*.075,.73),(u+sign*.09,.36+j*.047),(u+sign*.025,.34+j*.047)],'Temple_Turquoise' if j%2 else 'Temple_Limestone',.11)
    elif design=='scarab':
        disk(0,.46,.16,.22,'Temple_Turquoise',.12)
        disk(0,.73,.085,.075,'Temple_Bronze',.15)
        disk(0,.9,.09,.06,'Temple_Bronze',.16)
        for sign in [-1,1]:
            filled([(sign*.015,.28),(sign*.14,.39),(sign*.14,.56),(sign*.025,.65)],'Temple_Turquoise',.16)
    elif design=='watchful_eye':
        filled([(-.43,.58),(-.2,.73),(.06,.75),(.42,.56),(.12,.42),(-.18,.43)],'Temple_Limestone',.08)
        disk(.02,.58,.09,.12,'Temple_Turquoise',.17)
        disk(.02,.58,.035,.075,'Temple_Recess',.19)
    elif design=='papyrus':
        for j in [-1,0,1]:
            u=j*.22;v=.86-abs(j)*.06
            filled([(u,.68),(u-.13,v),(u-.06,v+.035),(u+.06,v+.035),(u+.13,v)],'Temple_Turquoise',.12)
    elif design=='constellation':
        for u,v in [(-.28,.78),(.12,.87),(.30,.54),(-.12,.40),(.12,.12)]:
            filled([(u-.065,v),(u-.02,v+.02),(u,v+.065),(u+.02,v+.02),(u+.065,v),(u+.02,v-.02),(u,v-.065),(u-.02,v-.02)],'Temple_Bronze',.14)
        disk(-.27,.22,.075,.055,'Temple_Turquoise',.12)
    elif design=='stepped_diamond':
        filled([(0,.92),(-.42,.5),(0,.08),(.42,.5)],'Temple_Limestone',.055)
        filled([(0,.79),(-.29,.5),(0,.21),(.29,.5)],'Temple_Turquoise',.10)
        filled([(0,.66),(-.16,.5),(0,.34),(.16,.5)],'Temple_Bronze',.14)
    else:
        for row in range(4):
            upper=[(-.43+j*.86/24,.15+row*.2+.055*math.sin(j*math.tau/12+row*.6)) for j in range(25)]
            filled(upper+[(u,v-.045) for u,v in reversed(upper)],'Temple_Turquoise' if row%2 else 'Temple_Limestone',.10)

    if design=='lotus':
        line([(-.36,.14),(.36,.14)],.075)
        for sign in [-1,1]:
            line([(0,.19),(sign*.29,.35),(sign*.39,.62),(sign*.16,.5),(0,.19)],.055,accent)
        line([(0,.19),(-.12,.55),(0,.84),(.12,.55),(0,.19)],.06)
        line([(0,.19),(0,.04)],.07)
    elif design=='winged_sun':
        oval(0,.65,.13,.12,accent)
        for sign in [-1,1]:
            for j in range(5):
                line([(sign*.12,.6-j*.035),(sign*(.25+j*.035),.69-j*.025),(sign*.43,.64-j*.075)],.035)
        line([(-.3,.26),(0,.13),(.3,.26)],.065,accent)
    elif design=='scarab':
        oval(0,.46,.16,.22,accent);oval(0,.73,.085,.07)
        line([(0,.25),(0,.66)],.04)
        for sign in [-1,1]:
            for j in range(3):
                line([(sign*.15,.34+j*.12),(sign*.32,.30+j*.15),(sign*.42,.4+j*.14)],.035)
        oval(0,.9,.08,.055,accent)
    elif design=='watchful_eye':
        line([(-.43,.58),(-.2,.73),(.06,.75),(.42,.56),(.12,.42),(-.18,.43),(-.43,.58)],.055)
        oval(.02,.58,.09,.12,accent)
        line([(.02,.43),(.02,.23),(-.16,.09),(-.29,.14),(-.24,.22)],.05)
        line([(.4,.55),(.22,.23)],.05,accent)
        line([(-.4,.85),(-.1,.92),(.22,.89),(.42,.79)],.055)
    elif design=='papyrus':
        for j in [-1,0,1]:
            line([(j*.07,.08),(j*.14,.42),(j*.22,.7)],.05,accent)
            for spread in [-.13,0,.13]:
                line([(j*.22,.68),(j*.22+spread,.88-abs(j)*.06)],.04)
        line([(-.35,.07),(.35,.07)],.06)
    elif design=='constellation':
        stars=[(-.28,.78),(.12,.87),(.30,.54),(-.12,.40),(.12,.12)]
        line(stars,.025,accent)
        for u,v in stars:
            line([(u-.045,v),(u+.045,v)],.04)
            line([(u,v-.035),(u,v+.035)],.04)
        oval(-.27,.22,.075,.055,accent)
    elif design=='stepped_diamond':
        for k in [.16,.29,.42]:
            line([(0,.5+k),(-k,.5),(0,.5-k),(k,.5),(0,.5+k)],.035,accent if k==.29 else 'Temple_Recess')
        for sign in [-1,1]:line([(sign*.42,.93),(sign*.26,.93),(sign*.26,.84)],.04)
    else:
        for row in range(4):
            line([(-.43+j*.86/24,.15+row*.2+.055*math.sin(j*math.tau/12+row*.6)) for j in range(25)],.045,accent if row%2 else 'Temple_Recess')
        for u in [-.35,0,.35]:line([(u-.05,.95),(u,.88),(u+.05,.95)],.035)
    # Border treatment varies; avoid identical framed tablets across the map.
    if serial%3==0:
        for sign in [-1,1]:
            line([(sign*.46,.08),(sign*.46,.02),(sign*.31,.02)],.045)
            line([(sign*.46,.92),(sign*.46,.98),(sign*.31,.98)],.045)
    region=original_region

def frieze(cx,cz,tx,tz,nx,nz,y,length,out=.12):
    yaw=math.atan2(tz,tx)
    box(local(cx,cz,tx,tz,nx,nz,0,y,out),(length,.46,.12),'Temple_Turquoise',yaw)
    for yy in [y-.30,y+.30]:box(local(cx,cz,tx,tz,nx,nz,0,yy,out+.02),(length,.12,.18),'Temple_Limestone',yaw)
    for j in range(int(length/.85)):
        s=-length/2+.4+j*.85
        # Alternating inset reed and solar marks, worn bronze against turquoise glaze.
        for ds,dy,w,h in [(0,0,.09,.30),(-.16,.03,.15,.065),(.16,-.03,.15,.065)]:
            box(local(cx,cz,tx,tz,nx,nz,s+ds,y+dy,out+.078),(w,h,.025),'Temple_Bronze',yaw)

# Courtyard at ground level: the large upper roofs of V1 do not exist in this model.
region='S_Courtyard';box((0,.62,0),(137,1.02,137),'Temple_Paving',check=False)
# Central sanctuary is a narrow longitudinal stepped spine, not a full-footprint pyramid.
for i,(w,d,bottom,top) in enumerate([(41,106,1.1,6),(33,72,6,14),(25,47,14,25),(20,28,25,37)]):
    region='S_Sanctuary_'+str(i);box((0,(top+bottom)/2,0),(w,top-bottom,d),'Temple_Sandstone',check=False)
    region='D_Sanctuary_'+str(i)
    for sx in [-1,1]:
        box((sx*(w/2+.18),top-.35,0),(.9,.65,d+.25))
        frieze(sx*w/2,0,0,1,sx,0,top-1.15,d)

# Independent platform towers follow the exact source deck outlines.
for part in parts:
    if not part['name'].endswith('_deck'):continue
    poly=hull(list(map(xyz,part['vertices'])));top=part['max']['y'];name=part['name'].replace('_deck','')
    cx=(part['min']['x']+part['max']['x'])/2;cz=(part['min']['z']+part['max']['z'])/2
    region='S_Pylon_'+name
    gallery_base=top-10.3
    if top>10:
        # Solid battered lower tower, recessed inner cell, actual open upper gallery, and stone roof.
        prism(poly,.4,gallery_base,scale_bottom=.975,scale_top=.975)
        prism(poly,.4,top-1.55,scale_bottom=.57,scale_top=.57)
        prism(poly,top-1.55,top-.22,scale_bottom=.99,scale_top=.99)
        for i,(ax,az) in enumerate(poly):
            bx,bz=poly[(i+1)%len(poly)];dx,dz=bx-ax,bz-az;length=math.hypot(dx,dz)
            if length<6:continue
            tx,tz=dx/length,dz/length;nx,nz=tz,-tx;mx,mz=(ax+bx)/2,(az+bz)/2
            for ss in [-length*.35,length*.35]:
                hh=top-1.55-.4
                box(local(mx,mz,tx,tz,nx,nz,ss,.4+hh/2,-.35),(1.3,hh,1.3),'Temple_Limestone',math.atan2(tz,tx),False)
    else:prism(poly,.4,top-.25,scale_bottom=1.015,scale_top=.975)
    region='D_Pylon_'+name
    # Horizontal entablature directly below the preserved deck, plus tapering vertical piers.
    for i,(ax,az) in enumerate(poly):
        bx,bz=poly[(i+1)%len(poly)];dx,dz=bx-ax,bz-az;length=math.hypot(dx,dz)
        tx,tz=dx/length,dz/length;nx,nz=tz,-tx;mx,mz=(ax+bx)/2,(az+bz)/2;yaw=math.atan2(tz,tx)
        for y,h,d,out in [(top-.30,.35,.85,.18),(top-.75,.35,.62,.13),(top-1.52,.18,.55,.12)]:
            box(local(mx,mz,tx,tz,nx,nz,0,y,out),(length+.1,h,d),yaw=yaw)
        if top<10:continue
        frieze(mx,mz,tx,tz,nx,nz,top-1.18,length,.17)
        base=gallery_base
        for s in [-length*.35,length*.35]:
            hh=top-1.65-base
            box(local(mx,mz,tx,tz,nx,nz,s,base+hh/2,.12),(1.1,hh,.6),yaw=yaw)
            box(local(mx,mz,tx,tz,nx,nz,s,base+.3,.2),(1.35,.6,.8),yaw=yaw)
            box(local(mx,mz,tx,tz,nx,nz,s,top-1.98,.2),(1.45,.6,.9),yaw=yaw)
            box(local(mx,mz,tx,tz,nx,nz,s,base+hh/2,.45),(.23,hh*.8,.07),'Temple_Turquoise',yaw)
        if length>7:
            # Inscribed shrine is on the recessed wall; the columns in front have real air behind them.
            wall_relief(cx+(mx-cx)*.57,cz+(mz-cz)*.57,tx,tz,nx,nz,base+.7,min(7,top-base-3),min(3.8,length*.30),.055)
            if gallery_base>9:
                apothem=(mx-cx)*nx+(mz-cz)*nz;slope=0
                wall_relief(mx,mz,tx,tz,nx,nz,2.0,min(8,gallery_base-3),min(3.8,length*.3),-apothem*.025+.045,slope)

# Concept architecture pass: engaged masonry details, never independent wall tablets.
# Source decks, stairs, structural solids and all C_ collision meshes stay unchanged.
architecture_towers=[]
for part in parts:
    if not part['name'].endswith('_deck') or part['max']['y']<10:continue
    poly=hull(list(map(xyz,part['vertices'])));top=part['max']['y']
    name=part['name'].replace('_deck','');architecture_towers.append(name)
    cx=(part['min']['x']+part['max']['x'])/2;cz=(part['min']['z']+part['max']['z'])/2
    gallery=top-10.3
    region='D_Architecture_Tower_'+name
    for i,(ax,az) in enumerate(poly):
        bx,bz=poly[(i+1)%len(poly)];length=math.hypot(bx-ax,bz-az)
        if length<6:continue
        tx,tz=(bx-ax)/length,(bz-az)/length;nx,nz=tz,-tx
        mx,mz=(ax+bx)/2,(az+bz)/2;yaw=math.atan2(tz,tx)
        apothem=(mx-cx)*nx+(mz-cz)*nz;wall=-apothem*.025
        def stone(s,y,w,h,d,out,mat='Temple_Limestone'):
            box(local(mx,mz,tx,tz,nx,nz,s,y,out),(w,h,d),mat,yaw)
        # Individual cornice blocks establish scale without covering the preserved roof plane.
        count=max(3,round(length/2.1));unit=length/count
        for j in range(count):
            ss=-length/2+(j+.5)*unit
            stone(ss,top-.42,unit-.055,.38,1.12,.10)
            stone(ss,top-1.84,unit-.045,.27,.96,.06)
            stone(ss,top-2.08,unit-.045,.18,.70,-.04,'Temple_Bronze')
        # A deep turquoise fascia sits directly against the existing solid roof edge.
        stone(0,top-1.14,length-.18,.38,.12,.51,'Temple_Turquoise')
        for j in range(max(3,round(length/.9))):
            ss=-length/2+.4+j*.9
            if ss>length/2-.2:continue
            stone(ss,top-1.63,.24,.23,.28,.44)
        # The broad lower wall is grounded; stepped base courses remain wall-attached.
        for yy,hh,dd,out in [(1.32,.38,.76,wall+.12),(1.69,.24,.54,wall+.08),(1.91,.13,.40,wall+.06)]:
            for j in range(count):stone(-length/2+(j+.5)*unit,yy,unit-.05,hh,dd,out)
        if gallery>4:
            stone(0,2.10,length-.2,.15,.08,wall+.18,'Temple_Turquoise')
        # Capitals and bases on existing gallery columns, plus fine engaged shaft ribs.
        for ss in [-length*.35,length*.35]:
            for yy,hh,ww,dd in [(top-2.35,.26,1.85,1.5),(top-2.65,.30,1.52,1.28),
                                  (gallery+.20,.35,1.68,1.50),(gallery+.54,.24,1.43,1.27)]:
                stone(ss,yy,ww,hh,dd,-.12)
            stone(ss,top-2.85,1.4,.13,.09,.51,'Temple_Bronze')
            stone(ss,gallery+.77,1.37,.16,.09,.51,'Temple_Turquoise')
            low=max(2.4,gallery+.95);high=top-3.10
            if high>low:
                for offset in [-.43,.43]:stone(ss+offset,(low+high)/2,.10,high-low,.08,.44)
        # Frame the lower masonry field with grounded pilasters, leaving the central field quiet.
        if gallery>5:
            h=gallery-2.45
            for ss in [-length*.25,length*.25]:
                stone(ss,2.25+h/2,.45,h,.20,wall+.03)
                stone(ss,2.29,.72,.22,.30,wall+.04)
                stone(ss,gallery-.28,.72,.22,.30,wall+.04)
            stone(0,gallery-.12,length*.54,.24,.24,wall+.03)

# Central spine receives shallow architectural bays and layered cornices on its real walls.
for tier,(w,d,bottom,top) in enumerate([(41,106,1.1,6),(33,72,6,14),(25,47,14,25),(20,28,25,37)]):
    region='D_Architecture_Sanctuary_'+str(tier)
    for sx in [-1,1]:
        face=sx*w/2
        for j in range(max(2,round(d/2.4))):
            n=max(2,round(d/2.4));unit=d/n;z=-d/2+(j+.5)*unit
            box((face+sx*.10,top-.65,z),(.72,.30,unit-.055))
            box((face+sx*.06,bottom+.28,z),(.50,.28,unit-.055))
        box((face+sx*.18,top-1.60,0),(.10,.18,d-.4),'Temple_Turquoise')
        bays=max(2,round(d/10));spacing=d/bays
        for j in range(bays):
            z=-d/2+(j+.5)*spacing;low=bottom+.7;high=top-2.1
            if high-low<1.5:continue
            # Nested shallow stone surrounds imply a recessed field, with the existing wall as backing.
            for dz in [-spacing*.34,spacing*.34]:
                box((face+sx*.07,(low+high)/2,z+dz),(.26,high-low,.38))
                box((face+sx*.09,low+.12,z+dz),(.35,.24,.65))
                box((face+sx*.09,high-.12,z+dz),(.35,.24,.65))
            box((face+sx*.07,high,z),(.26,.24,spacing*.72))
            box((face+sx*.07,low,z),(.26,.20,spacing*.72))
            if tier>=2:
                box((face+sx*.20,high-.37,z),(.06,.12,spacing*.58),'Temple_Bronze')

# Monumental carved fields attach to the exact scaled tower wall faces.
# Use the same polygon centroid as prism(), not the bounding-box midpoint.
for index,part in enumerate(p for p in parts if p['name'].endswith('_deck') and p['max']['y']>=25.9):
    poly=hull(list(map(xyz,part['vertices'])));cx=sum(x for x,z in poly)/len(poly);cz=sum(z for x,z in poly)/len(poly)
    top=part['max']['y'];gallery=top-10.3;name=part['name'].replace('_deck','')
    styles=['lotus','papyrus','winged_sun','watchful_eye','scarab','constellation','river']
    for i,(ax,az) in enumerate(poly):
        bx,bz=poly[(i+1)%len(poly)];length=math.hypot(bx-ax,bz-az)
        if length<6:continue
        tx,tz=(bx-ax)/length,(bz-az)/length;nx,nz=tz,-tx
        mx=cx+((ax+bx)/2-cx)*.975;mz=cz+((az+bz)/2-cz)*.975
        region='D_Monumental_'+name+'_'+str(i)
        primary={'CORE':4,'L3_NE':0,'L3_NW':1,'L3_SE':3,'L3_SW':2,'T7':6,'T8':5}[name]
        style=styles[(primary+i%2)%len(styles)];width=length*.37;height=gallery-3.6
        wall_relief(mx,mz,tx,tz,nx,nz,2.8,height,width,.045,0,style)
        monumental_reliefs.append(dict(tower=name,face=i,design=style,width=width))
# Distinct large winged and solar emblems on the upper recessed cell of the central tower.
part=next(p for p in parts if p['name']=='CORE_deck')
poly=hull(list(map(xyz,part['vertices'])));cx=sum(x for x,z in poly)/len(poly);cz=sum(z for x,z in poly)/len(poly)
for i,(ax,az) in enumerate(poly):
    bx,bz=poly[(i+1)%len(poly)];length=math.hypot(bx-ax,bz-az)
    if length<6:continue
    tx,tz=(bx-ax)/length,(bz-az)/length;nx,nz=tz,-tx
    mx=cx+((ax+bx)/2-cx)*.57;mz=cz+((az+bz)/2-cz)*.57
    region='D_Monumental_CORE_Cell_'+str(i)
    wall_relief(mx,mz,tx,tz,nx,nz,44.5,7.0,length*.40,.045,0,'scarab' if i%2 else 'winged_sun')
    monumental_reliefs.append(dict(tower='CORE_Cell',face=i,design='scarab' if i%2 else 'winged_sun'))
# Selected sanctuary bays carry emblems, leaving alternate fields quiet.
for tier,(w,d,bottom,top) in enumerate([(41,106,1.1,6),(33,72,6,14),(25,47,14,25),(20,28,25,37)]):
    if tier==0:continue
    bays=max(2,round(d/10));spacing=d/bays
    for sx in [-1,1]:
        for j in range(bays):
            if (j+tier)%2:continue
            z=-d/2+(j+.5)*spacing;region='D_Monumental_Sanctuary_'+str(tier)
            style=['papyrus','watchful_eye','stepped_diamond','scarab'][(j+tier+(sx>0))%4]
            wall_relief(sx*w/2,z,0,1,sx,0,bottom+.9,top-bottom-3.3,spacing*.48,.045,0,style)
            monumental_reliefs.append(dict(tower='Sanctuary_'+str(tier),bay=j,side=sx,design=style))

# Solid visual risers: keep collision on the original smooth ramps.
for part in parts:
    if not part['name'].startswith('TREADS_FLUSH') or part['max']['y']-part['min']['y']<.5:continue
    vv=list(map(xyz,part['vertices']));levels={}
    for j in range(0,len(part['triangles']),3):
        face=[vv[i] for i in part['triangles'][j:j+3]]
        normal=(Vector(face[1])-Vector(face[0])).cross(Vector(face[2])-Vector(face[0]))
        if normal.length<1e-8 or normal.normalized().y<.999:continue
        level=round(sum(v[1] for v in face)/3,3)
        levels.setdefault(level,[]).extend(face)
    region='V_ClosedRisers_'+part['name'][13:]
    for level,points in levels.items():
        poly=hull(points)
        if len(poly)<3:continue
        prism(poly,level-.85,level-.018,'Temple_Limestone')
        stair_closures+=1

# Thin sloping stair undersides with actual air between piers. Source tread meshes stay in Unity.
flights=[]
for part in parts:
    if not part['name'].startswith('TREADS_FLUSH'):continue
    points=list(map(xyz,part['vertices']));lo=min(p[1] for p in points);hi=max(p[1] for p in points)
    low=[p for p in points if p[1]<lo+.10];high=[p for p in points if p[1]>hi-.10]
    if hi-lo<.5:
        # Ground-level outer walks need only a low supporting plinth.
        region='S_BaseWalk_'+part['name'][13:];poly=hull(points);prism(poly,.25,1.75);continue
    a=Vector(tuple(sum(p[i] for p in low)/len(low) for i in range(3)));b=Vector(tuple(sum(p[i] for p in high)/len(high) for i in range(3)))
    delta=b-a;length=math.hypot(delta.x,delta.z);tx,tz=delta.x/length,delta.z/length;nx,nz=tz,-tx
    width=max(abs((p[0]-a.x)*nx+(p[2]-a.z)*nz) for p in points)
    flights.append((part,a,b,width))
    region='S_Flight_'+part['name'][13:]
    vv=[]
    for d in [-1.15,-.38]:
        for p,sign in [(a,-1),(a,1),(b,1),(b,-1)]:vv.append((p.x+nx*width*sign,p.y+d,p.z+nz*width*sign))
    add(vv,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'Temple_Sandstone')
    if hi<5:continue
    for j in range(1,max(2,int(length/7))):
        t=j/max(2,int(length/7));p=a+delta*t;end=p.y-1.15
        for side in [-1,1]:
            px,pz=p.x+nx*side*(width-.8),p.z+nz*side*(width-.8)
            base=1.13
            for ww,dd,y in [(41,106,6),(33,72,14),(25,47,25),(20,28,37)]:
                if abs(px)<ww/2 and abs(pz)<dd/2:base=max(base,y)
            hh=end-base
            if hh<2:continue
            region='S_Pier_'+part['name'][13:]+'_'+str(j)+'_'+str(side)
            box((px,base+hh/2,pz),(1.55,hh,1.55),'Temple_Limestone',math.atan2(tz,tx),False)
            region='D_Pier_'+part['name'][13:]
            box((px,end-.24,pz),(2.0,.48,2.0))
            box((px,base+.3,pz),(1.95,.6,1.95))
            box((px,base+.79,pz),(1.60,.2,1.60),'Temple_Turquoise')

# Closed battered precinct walls. Central south/north gates and all original corner routes remain open.
for side in range(4):
    angle=side*math.pi/2;tx,tz=math.cos(angle),math.sin(angle);nx,nz=tz,-tx;cx,cz=nx*70.8,nz*70.8
    for k,(start,end) in enumerate([(-58,-7),(7,58)] if side%2==0 else [(-58,58)]):
        length=end-start;s=(start+end)/2;mx,mz=cx+tx*s,cz+tz*s
        region='S_Wall_'+str(side)+'_'+str(k)
        poly=[(mx-tx*length/2-nx*1.75,mz-tz*length/2-nz*1.75),(mx+tx*length/2-nx*1.75,mz+tz*length/2-nz*1.75),(mx+tx*length/2+nx*1.75,mz+tz*length/2+nz*1.75),(mx-tx*length/2+nx*1.75,mz-tz*length/2+nz*1.75)]
        box((mx,4.6,mz),(length,9.2,3.5),'Temple_Sandstone',angle,False)
        region='D_Wall_'+str(side)
        # Separate coping stones with shallow chipped top corners, within the original cap envelope.
        units=max(2,round(length/2.5));unit=(length+.3)/units
        for q in range(units):
            ss=-(length+.3)/2+(q+.5)*unit;ww=unit-.035
            vv=[]
            for yy in [8.875,9.625]:
                for u,v in [(-ww/2,-2.175),(ww/2,-2.175),(ww/2,2.175),(-ww/2,2.175)]:
                    chip=.10 if yy>9 and (q+side)%7==0 and u<0 and v>0 else 0
                    vv.append(local(mx,mz,tx,tz,nx,nz,ss+u,yy-chip,.25+v))
            add(vv,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],
                'Temple_Sandstone' if q%9==4 else 'Temple_Limestone',True)
        frieze(mx,mz,tx,tz,nx,nz,8.25,length,1.83)
        # Long wall panels, not a colonnade around every floor.
        for j in range(max(1,int(length/9))):
            ss=-length/2+4.5+j*9
            wall_relief(mx+tx*ss,mz+tz*ss,tx,tz,nx,nz,1.1,6.0,4.2,1.80)
            for ds in [-3.9,3.9]:box(local(mx,mz,tx,tz,nx,nz,ss+ds,4.5,1.93),(.75,8.1,.75),yaw=angle)

# Entrance pylons frame the axial gate; corner pylons flank rather than cover the fixed corner stairs.
for sz in [-1,1]:
    for sx in [-1,1]:
        for x,z in [(sx*73,sz*61.5),(sx*61.5,sz*73),(sx*8.9,sz*71.5)]:
            key=str(x)+'_'+str(z);region='S_GatePylon_'+key
            top=12.6 if abs(x)<20 else 11.4
            poly=[(x-2.6,z-2.6),(x+2.6,z-2.6),(x+2.6,z+2.6),(x-2.6,z+2.6)]
            prism(poly,.3,top,scale_bottom=1.0,scale_top=1.0)
            region='D_GatePylon_'+key
            box((x,top+.25,z),(5.4,.65,5.4))
            for nx,nz in [(1,0),(-1,0),(0,1),(0,-1)]:
                tx,tz=-nz,nx
                frieze(x+nx*2.24,z+nz*2.24,tx,tz,nx,nz,top-.8,4.45)
                wall_relief(x+nx*2.60,z+nz*2.60,tx,tz,nx,nz,2.0,7.4,2.25,.045,0)
    region='S_GateLintel_'+str(sz);box((0,11.95,sz*71.5),(13.2,1.9,4.7),'Temple_Sandstone',check=False)
    region='D_GateLintel_'+str(sz);box((0,13.1,sz*71.5),(16.5,.65,5.3));frieze(0,sz*73.9,1,0,0,sz,12,13)

# Limited true galleries along the inner north/south enclosure, with open bays and visible paving.
for sz in [-1,1]:
    for sx in [-1,1]:
        region='S_CourtPortico_'+str(sx)+'_'+str(sz)
        box((sx*35,7.3,sz*66),(35,.9,5.8),'Temple_Limestone',check=False)
        for j in range(7):
            x=sx*(19.2+j*5.25);z=sz*63.8
            box((x,4,z),(1.25,5.8,1.25),'Temple_Limestone',check=False)
            region='D_Portico';box((x,6.8,z),(1.8,.55,1.8));box((x,1.43,z),(1.8,.55,1.8))
            region='S_CourtPortico_'+str(sx)+'_'+str(sz)

# Concept enclosure pass: attached facade trim with all structural/collision meshes unchanged.
enclosure_details={'wall_runs':6,'gate_pylons':12,'axial_gates':2,'porticos':4,'portico_columns':28}
for side in range(4):
    angle=side*math.pi/2;tx,tz=math.cos(angle),math.sin(angle);nx,nz=tz,-tx;cx,cz=nx*70.8,nz*70.8
    for k,(start,end) in enumerate([(-58,-7),(7,58)] if side%2==0 else [(-58,58)]):
        length=end-start;center=(start+end)/2;mx,mz=cx+tx*center,cz+tz*center
        region='D_Architecture_Enclosure_'+str(side)+'_'+str(k)
        n=max(2,round(length/2.5));unit=length/n
        for j in range(n):
            ss=-length/2+(j+.5)*unit
            for out in [-1.78,1.78]:
                box(local(mx,mz,tx,tz,nx,nz,ss,1.37,out),(unit-.045,.38,.24),'Temple_Limestone',angle)
                box(local(mx,mz,tx,tz,nx,nz,ss,8.94,out),(unit-.045,.20,.22),'Temple_Limestone',angle)
        # The existing external piers receive caps/bases instead of new freestanding columns.
        for j in range(max(1,int(length/9))):
            ss=-length/2+4.5+j*9
            for ds in [-3.9,3.9]:
                for yy,hh,ww in [(1.49,.34,.95),(1.82,.18,.83),(7.91,.20,.93),(8.08,.16,1.03)]:
                    box(local(mx,mz,tx,tz,nx,nz,ss+ds,yy,1.96),(ww,hh,.82),'Temple_Limestone',angle)

for sz in [-1,1]:
    for sx in [-1,1]:
        for x,z in [(sx*73,sz*61.5),(sx*61.5,sz*73),(sx*8.9,sz*71.5)]:
            top=12.6 if abs(x)<20 else 11.4
            region='D_Architecture_Gateway_'+str(x)+'_'+str(z)
            # Recessed-edge trims stay on front/back faces, clear of the jamb opening.
            for facing in [-1,1]:
                zz=z+facing*2.62
                for dx in [-2.20,2.20]:
                    box((x+dx,6.2,zz),(.26,9.1,.20))
                    box((x+dx,1.73,zz),(.48,.30,.28))
                    box((x+dx,10.7,zz),(.48,.28,.28))
                box((x,1.44,zz),(5.1,.30,.24))
                box((x,1.76,zz),(5.05,.18,.18))
                box((x,top-1.5,zz),(5.0,.20,.20))
                box((x,top-1.23,zz),(5.0,.12,.10),'Temple_Bronze')
            # Four discrete crown courses, none taller than the preserved upper silhouette.
            for dx in [-1.35,1.35]:
                for dz in [-1.35,1.35]:
                    box((x+dx,top+.47,z+dz),(2.66,.15,2.66))
            for facing in [-1,1]:
                box((x,top-.15,z+facing*2.58),(5.15,.24,.32))
    # Layered axial portal surrounds fixed to the original lintel; clear height unchanged.
    region='D_Architecture_Portal_'+str(sz)
    for face in [-1,1]:
        z=sz*71.5+face*2.42
        for j in range(7):
            box((-5.7+j*1.9,12.69,z),(1.86,.40,.25))
        box((0,11.26,z),(12.5,.20,.20))
        box((0,12.93,z),(13.0,.12,.24),'Temple_Bronze')
        # A shallow decorative top frieze keeps the existing doorway open.
        for j in range(13):box((-5.7+j*.95,11.58,z),(.18,.20,.13))

# Four existing porticos: articulate their beams, capitals and shafts without adding columns.
for sz in [-1,1]:
    for sx in [-1,1]:
        region='D_Architecture_Portico_'+str(sx)+'_'+str(sz)
        for j in range(14):
            x=sx*35-17.5+(j+.5)*2.5
            box((x,7.50,sz*63.16),(2.46,.30,.16))
            box((x,7.09,sz*63.16),(2.46,.17,.12),'Temple_Turquoise')
        for j in range(7):
            x=sx*(19.2+j*5.25);z=sz*63.8
            # All widened bases/capitals remain within the pre-existing 1.8m footprint.
            box((x,1.64,z),(1.74,.12,1.74))
            box((x,1.82,z),(1.43,.22,1.43))
            box((x,6.54,z),(1.46,.18,1.46))
            box((x,6.72,z),(1.76,.14,1.76))
            for face in [-1,1]:
                zz=z+face*.625
                box((x,2.04,zz),(1.17,.13,.08),'Temple_Bronze')
                box((x,6.32,zz),(1.17,.12,.08),'Temple_Turquoise')
                for dx in [-.40,.40]:box((x+dx,4.15,zz),(.095,3.9,.08))

# Uneven rubble and shallow sand aprons integrate walls with the ground; gameplay clearance is sampled.
for side in range(4):
    angle=side*math.pi/2;tx,tz=math.cos(angle),math.sin(angle);nx,nz=tz,-tx
    for inside in [False,True]:
        region='D_SandApron_'+str(side)+'_'+str(inside);vv=[];ff=[];rows=117;cols=13
        for j in range(rows):
            s=-58+j;envelope=math.sin(math.pi*j/(rows-1))**.4
            for k in range(cols):
                d=k*.45
                outward=(1 if not inside else -1)
                radial=(72.50 if not inside else 69.06)+outward*d
                amplitude=(.85 if not inside else .66)*(.65+.35*math.sin(s*.19+side)**2)*envelope
                taper=math.cos(k/(cols-1)*math.pi/2)**2
                yy=(.74 if not inside else 1.135)+amplitude*taper*(.9+.10*math.sin(s*.43+d*2))
                vv.append((nx*radial+tx*s,yy,nz*radial+tz*s))
                if j and k:
                    q=j*cols+k;ff.append((q-cols-1,q-cols,q,q-1))
        ff=[f if (Vector(vv[f[1]])-Vector(vv[f[0]])).cross(Vector(vv[f[2]])-Vector(vv[f[0]])).y>0 else tuple(reversed(f)) for f in ff]
        add(vv,ff,'Temple_Sand')

for j in range(160):
    side=j%4;ang=side*math.pi/2;tx,tz=math.cos(ang),math.sin(ang);nx,nz=tz,-tx;s=random.uniform(-66,66)
    x,z=nx*random.uniform(72,79)+tx*s,nz*random.uniform(72,79)+tz*s
    region='D_Rubble';w=random.uniform(.3,1.2);box((x,random.uniform(.7,1.1),z),(w,random.uniform(.2,.65),w*.75),'Temple_Sandstone',random.uniform(0,math.pi))

for cx,cz in [(-37,-77),(34,-77),(-76,30),(76,-33)]:
    for j in range(16):
        x,z=cx+random.gauss(0,2.6),cz+random.gauss(0,1.7);w=random.uniform(.6,2.0);h=random.uniform(.3,.85);yaw=random.uniform(0,math.pi)
        cs,sn=math.cos(yaw),math.sin(yaw);vv=[]
        for dy in [-h/2,h/2]:
            for px,pz in [(-w/2,-w*.4),(w/2,-w*.4),(w/2,w*.4),(-w/2,w*.4)]:
                px+=random.uniform(-.12,.12);pz+=random.uniform(-.12,.12)
                vv.append((x+px*cs-pz*sn,.82+dy+random.uniform(-.1,.1),z+px*sn+pz*cs))
        region='D_FracturedMasonry';add(vv,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'Temple_Sandstone',True)

def make_object(name,verts,faces,mat=None):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    if mat:mesh.materials.append(mats[mat])
    return obj

# Convex cutters reserve the unchanged walking surfaces plus shoulders. They cut only new masonry.
cutters=[]
for part in parts:
    points=list(map(xyz,part['vertices']));poly=hull(points)
    bottom=part['min']['y']-.4
    if part['name'].endswith('_deck'):
        heights=[part['max']['y']-.20]*len(poly)
    else:
        flight=next((f for f in flights if f[0]['name']==part['name']),None)
        if flight:
            _,a,b,width=flight;d=b-a;l2=d.x*d.x+d.z*d.z
            heights=[a.y+((x-a.x)*d.x+(z-a.z)*d.z)/l2*d.y-.48 for x,z in poly]
        else:heights=[bottom]*len(poly)
    cx=sum(x for x,z in poly)/len(poly);cz=sum(z for x,z in poly)/len(poly)
    # A narrow shoulder handles floating-point edge probes without changing source meshes.
    poly=[(x+(x-cx)*.035,z+(z-cz)*.035) for x,z in poly]
    n=len(poly);vs=[coord((x,h,z)) for (x,z),h in zip(poly,heights)]+[coord((x,80,z)) for x,z in poly]
    fs=[tuple(range(n)),tuple(range(n*2-1,n-1,-1))]+[(i,i+n,(i+1)%n+n,(i+1)%n) for i in range(n)]
    cutter=make_object('CUT_'+part['name'],vs,fs);cutters.append(cutter)

def intersects(a,b):
    aa=[a.matrix_world@Vector(v) for v in a.bound_box];bb=[b.matrix_world@Vector(v) for v in b.bound_box]
    return all(min(v[i] for v in aa)<max(v[i] for v in bb) and max(v[i] for v in aa)>min(v[i] for v in bb) for i in range(3))
objects=[];booleans=0
for (name,mat),(verts,faces) in groups.items():
    if not faces:continue
    obj=make_object(name+'_'+mat,verts,faces,mat);bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    if name.startswith(('S_','D_Pylon','D_GatePylon','D_Architecture','D_CustomMotif')):
        for cutter in cutters:
            if not intersects(obj,cutter):continue
            mod=obj.modifiers.new('Preserve gameplay clearance','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
            bpy.ops.object.modifier_apply(modifier=mod.name);booleans+=1
        if name.startswith('S_Wall_'):
            side=int(name.split('_')[2]);angle=side*math.pi/2;tx,tz=math.cos(angle),math.sin(angle);nx,nz=tz,-tx
            samples=[v.co.x*tx-v.co.y*tz for v in obj.data.vertices];lower,upper=min(samples)+2,max(samples)-2
            for j in range(5):
                s=lower+(upper-lower)*(j+.4)/5;yy=1.25*random.randint(1,5)+random.uniform(-.25,.25)
                point=coord((nx*72.68+tx*s,yy,nz*72.68+tz*s))
                bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=point);chip=bpy.context.object
                for vertex in chip.data.vertices:vertex.co*=random.uniform(.85,1.15)
                chip.scale=(random.uniform(.38,.7),random.uniform(.38,.65),random.uniform(.22,.43));chip.rotation_euler=(.3,.5,random.random())
                bpy.context.view_layer.objects.active=obj
                mod=obj.modifiers.new('Eroded missing stone','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=chip
                bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(chip,do_unlink=True)
    if len(obj.data.polygons):
        if name.startswith('S_'):
            collision=obj.copy();collision.data=obj.data.copy();collision.name='C_'+obj.name[2:]
            bpy.context.collection.objects.link(collision);objects.append(collision)
            collision.hide_render=True;collision.display_type='WIRE'
        if not name.startswith(('D_SandApron','V_ClosedRisers','D_CustomMotif')):
            bevel=obj.modifiers.new('Worn stone arris','BEVEL');bevel.width=.065 if name.startswith('S_') else .018;bevel.segments=2 if name.startswith('S_') else 1
            bpy.ops.object.modifier_apply(modifier=bevel.name)
        # Weighted normals retain broad stone planes after beveling.
        normal=obj.modifiers.new('Stone face normals','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=35
        bpy.ops.object.modifier_apply(modifier=normal.name)
        objects.append(obj)
    obj.select_set(False)

# Separate authored relief objects retain their source UV maps and individual textures.
templates={}
for kind in ['wall-deco','wall-deco-anubis','god-la']:
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.wm.obj_import(filepath=str(ROOT/'Docs/model-art'/(kind+'.obj')))
    obj=bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    material=obj.data.materials[0];material.name='Relief_'+kind
    templates[kind]=obj
for i,p in enumerate(relief_placements):
    if p is None:continue
    template=templates[p['kind']];obj=template.copy();obj.data=template.data.copy()
    obj.name='D_AuthoredRelief_'+str(i)+'_'+p['kind'];bpy.context.collection.objects.link(obj)
    lo=[min(v.co[j] for v in obj.data.vertices) for j in range(3)]
    hi=[max(v.co[j] for v in obj.data.vertices) for j in range(3)]
    scale=min(p['width']*.82/(hi[0]-lo[0]),p['height']*.82/(hi[2]-lo[2]))
    hh=(hi[2]-lo[2])*scale
    for v in obj.data.vertices:
        lateral=(v.co.x-(lo[0]+hi[0])/2)*scale
        height=(v.co.z-lo[2])*scale+(p['height']-hh)/2
        # Source OBJ faces toward -Y. Its back lies at max Y.
        out=p['out']+.12+(hi[1]-v.co.y)*scale*.55-p['slope']*height
        v.co=coord(local(p['cx'],p['cz'],p['tx'],p['tz'],p['nx'],p['nz'],lateral,p['base']+height,out))
    # Mapping the authored forward axis onto the outward wall normal mirrors the mesh.
    # Reverse its winding as well, preserving UV loops and the front relief surface.
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    bpy.context.view_layer.objects.active=obj
    for cutter in cutters:
        if not intersects(obj,cutter):continue
        mod=obj.modifiers.new('Route clearance','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
    objects.append(obj)
for obj in templates.values():bpy.data.objects.remove(obj,do_unlink=True)


statue_records=[]
for kind,x,z,height,yaw in [
    ('la-dragon',-34,-43,4.3,0),('osiris-dragon',34,-43,4.3,0),
    ('obelisk-giant',-42,42,7,0)]:
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.wm.obj_import(filepath=str(ROOT/'Docs/model-art'/(kind+'.obj')))
    obj=bpy.context.selected_objects[0];bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    lo=[min(v.co[j] for v in obj.data.vertices) for j in range(3)]
    hi=[max(v.co[j] for v in obj.data.vertices) for j in range(3)]
    scale=height/(hi[2]-lo[2])
    for v in obj.data.vertices:v.co=Vector(((v.co.x-(lo[0]+hi[0])/2)*scale,(v.co.y-(lo[1]+hi[1])/2)*scale,(v.co.z-lo[2])*scale))
    obj.location=(x,-z,2.13);obj.rotation_euler.z=yaw
    obj.name='D_UniqueStatue_'+kind
    obj.data.materials[0].name='Relief_'+kind
    objects.append(obj)
    collision=obj.copy();collision.data=obj.data.copy();collision.name='C_UniqueStatue_'+kind
    bpy.context.collection.objects.link(collision);collision.hide_render=True;collision.display_type='WIRE';objects.append(collision)
    # A separate solid base connects the sculpture to courtyard paving.
    size=max(2.8,(hi[0]-lo[0])*scale+.5);depth=max(2.8,(hi[1]-lo[1])*scale+.5)
    verts=[coord((x+dx,yy,z+dz)) for dx,yy,dz in [
        (-size/2,1.12,-depth/2),(size/2,1.12,-depth/2),(size/2,2.13,-depth/2),(-size/2,2.13,-depth/2),
        (-size/2,1.12,depth/2),(size/2,1.12,depth/2),(size/2,2.13,depth/2),(-size/2,2.13,depth/2)]]
    plinth=make_object('S_StatuePlinth_'+kind,verts,BOXF,'Temple_Sandstone');objects.append(plinth)
    col=plinth.copy();col.data=plinth.data.copy();col.name='C_StatuePlinth_'+kind
    bpy.context.collection.objects.link(col);col.hide_render=True;col.display_type='WIRE';objects.append(col)
    statue_records.append(dict(model=kind,x=x,z=z,height=height))

for cutter in cutters:bpy.data.objects.remove(cutter,do_unlink=True)
weather_path=OUT/'StoneWeather.png'
if weather_path.exists():
    weather_image=bpy.data.images.load(str(weather_path),check_existing=True);weather_image.colorspace_settings.name='Non-Color';weather_image.pack()
    for name,mat in mats.items():
        mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
        geometry=nodes.new('ShaderNodeNewGeometry');scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=.18;links.new(geometry.outputs['Position'],scale.inputs[0])
        tex=nodes.new('ShaderNodeTexImage');tex.image=weather_image;tex.projection='BOX';tex.projection_blend=.18;links.new(scale.outputs[0],tex.inputs['Vector'])
        channels=nodes.new('ShaderNodeSeparateColor');channels.mode='RGB';links.new(tex.outputs['Color'],channels.inputs[0])
        variation=nodes.new('ShaderNodeMath');variation.operation='MULTIPLY_ADD';variation.inputs[1].default_value=.65;variation.inputs[2].default_value=.675;links.new(channels.outputs['Red'],variation.inputs[0])
        tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[1].default_value=(*palette[name],1);links.new(variation.outputs[0],tint.inputs[2])
        principled=nodes.new('ShaderNodeBsdfPrincipled');links.new(tint.outputs[0],principled.inputs['Base Color']);principled.inputs['Roughness'].default_value=.86
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.65;bump.inputs['Distance'].default_value=.055;links.new(channels.outputs['Green'],bump.inputs['Height']);links.new(bump.outputs[0],principled.inputs['Normal'])
        output=nodes.new('ShaderNodeOutputMaterial');links.new(principled.outputs[0],output.inputs[0])
reference_collection=bpy.data.collections.new('REFERENCE - original decks and treads - locked');bpy.context.scene.collection.children.link(reference_collection)
for part in parts:
    vv=list(map(coord,map(xyz,part['vertices'])));ff=[part['triangles'][j:j+3] for j in range(0,len(part['triangles']),3)]
    ref=make_object('REFERENCE_'+part['name'],vv,ff,'Temple_Limestone');bpy.context.collection.objects.unlink(ref);reference_collection.objects.link(ref);ref.hide_render=True;ref.hide_select=True;ref.display_type='WIRE'
reference_collection.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:obj.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'Temple_Courtyard_v2.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'Temple_Courtyard_v2.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
report={'enclosure_details':enclosure_details,'monumental_reliefs':monumental_reliefs,'architecture_towers':architecture_towers,'architecture_sanctuary_tiers':4,'imported_wall_reliefs':len([p for p in relief_placements if p]),'custom_motifs':custom_motifs,'closed_risers':stair_closures,'unique_statues':statue_records,'objects':len(objects),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),'rejected_decorations':skipped,'clearance_boolean_cuts':booleans,'platforms':17,'protected_surface_triangles':len(pf)}
(DOC/'model-report.json').write_text(json.dumps(report,indent=2));print('COURTYARD_MODEL_COMPLETE',report)
