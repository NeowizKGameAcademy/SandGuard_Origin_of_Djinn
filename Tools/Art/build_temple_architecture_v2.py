"""Blender-built courtyard temple. Unity gameplay decks/treads are reference-only.
Run with Blender --background --python Tools/Art/build_temple_architecture_v2.py.
"""
import bpy, bmesh, json, math, random
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path('C:/course/unity/SandGuard')
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
def wall_relief(cx,cz,tx,tz,nx,nz,base,height,width=2.5,out=0,slope=0):
    yaw=math.atan2(tz,tx)
    def B(s,y,o,w,h,d,mat='Temple_Limestone'):
        # Both edges follow the battered wall, avoiding a flat tablet occluding its own upper relief.
        verts=[local(cx,cz,tx,tz,nx,nz,s+ds,base+y+dy,out+o+dd-slope*(y+dy)) for ds,dy,dd in [(-w/2,-h/2,-d/2),(w/2,-h/2,-d/2),(w/2,h/2,-d/2),(-w/2,h/2,-d/2),(-w/2,-h/2,d/2),(w/2,-h/2,d/2),(w/2,h/2,d/2),(-w/2,h/2,d/2)]]
        return add(verts,BOXF,mat,True)
    B(0,height/2,0,width,height,.09,'Temple_Sandstone')
    for s in [-width/2,width/2]:B(s,height/2,.035,.09,height,.10)
    for y in [0,height]:B(0,y,.035,width,.09,.1)
    # Solar disc with a carved shadow line and a broad fan of feathered rays.
    r=min(width*.24,height*.115);sy=height*.78;vs=[];fs=[]
    for rr in [r,r*.82]:
        for j in range(40):
            a=math.tau*j/40;yy=sy+rr*math.sin(a);vs.append(local(cx,cz,tx,tz,nx,nz,rr*math.cos(a),base+yy,out+.082-slope*yy))
    fs.extend((j,(j+1)%40,(j+1)%40+40,j+40) for j in range(40));add(vs,fs,'Temple_Recess',True)
    # A relief strip follows the wall plane; use quads with local tangent offsets.
    def line(points,w,mat='Temple_Recess'):
        vv=[];ff=[]
        for j,(s,y) in enumerate(points):
            a=points[max(0,j-1)];b=points[min(len(points)-1,j+1)];dx,dy=b[0]-a[0],b[1]-a[1];ll=math.hypot(dx,dy)
            for depth in [.07,.10]:
                for sign in [-1,1]:vv.append(local(cx,cz,tx,tz,nx,nz,s+sign*dy/ll*w/2,base+y-sign*dx/ll*w/2,out+depth-slope*y))
            if j:
                k=j*4;f=k-4;ff.extend([(f,f+1,k+1,k),(f+2,k+2,k+3,f+3),(f,k,k+2,f+2),(f+1,f+3,k+3,k+1)])
        ff.extend([(0,2,3,1),(len(vv)-4,len(vv)-3,len(vv)-1,len(vv)-2)]);add(vv,ff,mat,True)
    for strand in [-2,-1,0,1,2]:
        line([(strand*width*.095+width*.06*math.sin(t*5+strand*.4),height*(.1+.52*t)) for t in [j/24 for j in range(25)]],.045)
    for sign in [-1,1]:
        for j in range(6):
            line([(sign*(width*.10+j*width*.055),height*.67),(sign*(width*.2+j*width*.055),height*(.62-j*.018))],.06)
    # Side borders carry small chisel-cut geometric marks, rather than a framed poster.
    for sign in [-1,1]:
        for j in range(max(3,int(height/.55))):
            B(sign*(width/2-.12),.25+j*.5,.075,.065,.19,.055,'Temple_Recess')
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
        prism(poly,.4,gallery_base,scale_bottom=1.10,scale_top=.975)
        prism(poly,gallery_base,top-1.55,scale_bottom=.57,scale_top=.57)
        prism(poly,top-1.55,top-.22,scale_bottom=.99,scale_top=.99)
        for i,(ax,az) in enumerate(poly):
            bx,bz=poly[(i+1)%len(poly)];dx,dz=bx-ax,bz-az;length=math.hypot(dx,dz)
            if length<6:continue
            tx,tz=dx/length,dz/length;nx,nz=tz,-tx;mx,mz=(ax+bx)/2,(az+bz)/2
            for ss in [-length*.35,length*.35]:
                hh=top-1.55-gallery_base
                box(local(mx,mz,tx,tz,nx,nz,ss,gallery_base+hh/2,-.35),(1.3,hh,1.3),'Temple_Limestone',math.atan2(tz,tx),False)
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
                apothem=(mx-cx)*nx+(mz-cz)*nz;slope=apothem*.125/(gallery_base-.4)
                wall_relief(mx,mz,tx,tz,nx,nz,2.0,min(8,gallery_base-3),min(3.8,length*.3),apothem*.10-1.6*slope+.045,slope)

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
        box((mx+nx*.25,9.25,mz+nz*.25),(length+.3,.75,4.35),yaw=angle)
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
            prism(poly,.3,top,scale_bottom=1.16,scale_top=.86)
            region='D_GatePylon_'+key
            box((x,top+.25,z),(5.4,.65,5.4))
            for nx,nz in [(1,0),(-1,0),(0,1),(0,-1)]:
                tx,tz=-nz,nx
                frieze(x+nx*2.24,z+nz*2.24,tx,tz,nx,nz,top-.8,4.45)
                wall_relief(x+nx*2.91,z+nz*2.91,tx,tz,nx,nz,2.0,7.4,2.25,.045,.067)
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
    if name.startswith('S_'):
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
        if not name.startswith('D_SandApron'):
            bevel=obj.modifiers.new('Worn stone arris','BEVEL');bevel.width=.065 if name.startswith('S_') else .018;bevel.segments=2 if name.startswith('S_') else 1
            bpy.ops.object.modifier_apply(modifier=bevel.name)
        # Weighted normals retain broad stone planes after beveling.
        normal=obj.modifiers.new('Stone face normals','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=35
        bpy.ops.object.modifier_apply(modifier=normal.name)
        objects.append(obj)
    obj.select_set(False)
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
report={'objects':len(objects),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),'rejected_decorations':skipped,'clearance_boolean_cuts':booleans,'platforms':17,'protected_surface_triangles':len(pf)}
(DOC/'model-report.json').write_text(json.dumps(report,indent=2));print('COURTYARD_MODEL_COMPLETE',report)
