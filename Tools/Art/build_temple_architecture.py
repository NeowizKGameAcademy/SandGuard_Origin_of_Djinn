"""Facade dressing in surveyed Unity world coordinates. Existing playable geometry is not exported or edited."""
import bpy, json, math, random
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path('C:/course/unity/SandGuard')
OUT=ROOT/'Assets/TempleArt/Architecture'
DOC=ROOT/'Docs/LevelArt/TempleArchitecture'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
reference=DOC/'structure-reference.json'
parts=json.loads((reference if reference.exists() else DOC/'survey.json').read_text())['parts']
random.seed(612)
def vec(v): return (v['x'],v['y'],v['z'])
pv=[];pf=[]
for p in parts:
    if not (p['name'].endswith('_deck') or p['name'].startswith('TREADS_FLUSH')):continue
    offset=len(pv); pv.extend([vec(v) for v in p['vertices']])
    for i in range(0,len(p['triangles']),3):
        inds=p['triangles'][i:i+3]
        a,b,c=[Vector(pv[offset+j]) for j in inds]
        if (b-a).cross(c-a).y>0.000001:pf.append(tuple(offset+j for j in inds))
protect=BVHTree.FromPolygons(pv,pf,all_triangles=True)
body_reference=DOC/'body-reference.json'
if body_reference.exists():body_part=json.loads(body_reference.read_text())
else:
    full=json.loads((DOC/'survey.json').read_text())['parts']
    body_part=next(p for p in full if p['name']=='PYRAMID_FILLED_TERRACES')
    body_reference.write_text(json.dumps(body_part,separators=(',',':')));del full
bv=[vec(v) for v in body_part['vertices']]
bt=[tuple(body_part['triangles'][i:i+3]) for i in range(0,len(body_part['triangles']),3)]
body_bvh=BVHTree.FromPolygons(bv,bt,all_triangles=True)

palette={'Facade_Sandstone':(.65,.49,.30),'Facade_Limestone':(.76,.62,.42),
         'Facade_Shadow':(.31,.235,.15),'Facade_Bronze':(.40,.28,.13),
         'Facade_Turquoise':(.075,.29,.27),'Facade_Relief':(.63,.46,.27)}
materials={}
for name,color in palette.items():
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);materials[name]=m
groups={}; region='Exterior'; skipped=0
def add(verts,faces,mat):
    key=(region,mat)
    vs,fs=groups.setdefault(key,([],[]));n=len(vs)
    # Geometric faces are authored with outward right-hand winding.
    vs.extend([(x,-z,y) for x,y,z in verts]);fs.extend([tuple(n+i for i in f) for f in faces])
def clear(verts):
    global skipped
    lo=[min(v[i] for v in verts) for i in range(3)];hi=[max(v[i] for v in verts) for i in range(3)]
    nx=max(1,math.ceil((hi[0]-lo[0])/1.25));nz=max(1,math.ceil((hi[2]-lo[2])/1.25))
    for a in range(nx+1):
        for b in range(nz+1):
            x=lo[0]+(hi[0]-lo[0])*a/nx;z=lo[2]+(hi[2]-lo[2])*b/nz
            hit=protect.ray_cast(Vector((x,hi[1]+3,z)),Vector((0,-1,0)),hi[1]-lo[1]+5)
            if hit[0] is not None and lo[1]-.15<=hit[0].y<=hi[1]+2:
                skipped+=1;return False
    return True
def box(center,size,mat='Facade_Limestone',yaw=0,check=True):
    c,s=math.cos(yaw),math.sin(yaw);x,y,z=center;w,h,d=[t/2 for t in size]
    vs=[(x+a*c-b*s,y+dy,z+a*s+b*c) for a,dy,b in [(-w,-h,-d),(w,-h,-d),(w,h,-d),(-w,h,-d),(-w,-h,d),(w,-h,d),(w,h,d),(-w,h,d)]]
    if check and not clear(vs):return False
    add(vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)],mat)
    return True
def cylinder(center,r,h,mat='Facade_Limestone',n=12):
    x,y,z=center;vs=[]
    for dy in (-h/2,h/2):
        for i in range(n):
            a=i*math.tau/n;vs.append((x+r*math.cos(a),y+dy,z+r*math.sin(a)))
    if not clear(vs):return
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
    fs.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n));add(vs,fs,mat)
def hull(points):
    points=sorted(set((round(p[0],3),round(p[2],3)) for p in points))
    def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lower=[];upper=[]
    for p in points:
        while len(lower)>=2 and cross(lower[-2],lower[-1],p)<=.0001:lower.pop()
        lower.append(p)
    for p in reversed(points):
        while len(upper)>=2 and cross(upper[-2],upper[-1],p)<=.0001:upper.pop()
        upper.append(p)
    return lower[:-1]+upper[:-1]
def local(cx,cz,tx,tz,nx,nz,s,y,out):return (cx+tx*s+nx*out,y,cz+tz*s+nz*out)
def relief(cx,cz,tx,tz,nx,nz,base,scale=1,outset=0):
    cx+=nx*outset;cz+=nz*outset
    yaw=math.atan2(tz,tx)
    def B(s,y,o,w,h,d,mat='Facade_Relief'):box(local(cx,cz,tx,tz,nx,nz,s,base+y*scale,o),(w*scale,h*scale,d),mat,yaw)
    B(0,2.5,.2,2.1,5.4,.16,'Facade_Shadow')
    for side in [-1,1]:B(side*1.12,2.5,.35,.16,5.65,.2)
    for y in [-.3,5.3]:B(0,y,.35,2.4,.18,.25,'Facade_Limestone')
    # Solar seal and flowing ribbons carved into a recessed tablet.
    vs=[];faces=[]
    for r in [.57,.43]:
        for i in range(24):
            a=i*math.tau/24;vs.append(local(cx,cz,tx,tz,nx,nz,math.cos(a)*r*scale,base+(3.75+math.sin(a)*r)*scale,.42))
    for i in range(24):faces.append((i,(i+1)%24,(i+1)%24+24,i+24))
    if clear(vs):add(vs,faces,'Facade_Bronze')
    def ribbon(points,width,mat='Facade_Relief'):
        vv=[];ff=[]
        for j,(s,y) in enumerate(points):
            a=points[max(0,j-1)];b=points[min(len(points)-1,j+1)]
            ds,dy=b[0]-a[0],b[1]-a[1];ll=math.hypot(ds,dy);px,py=dy/ll*width/2,-ds/ll*width/2
            for depth in [.37,.45]:
                for sign in [-1,1]:vv.append(local(cx,cz,tx,tz,nx,nz,(s+sign*px)*scale,base+(y+sign*py)*scale,depth))
            if j:
                k=j*4;prev=k-4
                ff.extend([(prev,prev+1,k+1,k),(prev+2,k+2,k+3,prev+3),(prev,k,k+2,prev+2),(prev+1,prev+3,k+3,k+1)])
        ff.extend([(0,2,3,1),(len(vv)-4,len(vv)-3,len(vv)-1,len(vv)-2)])
        if clear(vv):add(vv,ff,mat)
    for strand in [-1,0,1]:
        ribbon([(strand*.28+.16*math.sin(j*.18+strand*.9),.35+j*2.8/40) for j in range(41)],.075)
    for side in [-1,1]:
        ribbon([(side*(.18+t*.75),3.14-.7*t+.12*math.sin(t*math.pi)) for t in [j/20 for j in range(21)]],.09,'Facade_Bronze')

# Fascias are below the exact source decks, with decorated piers on visible faces.
for p in parts:
    if not p['name'].endswith('_deck'):continue
    poly=hull([vec(v) for v in p['vertices']]);top=p['max']['y'];region=p['name'].replace('_deck','_facade')
    for i,(ax,az) in enumerate(poly):
        bx,bz=poly[(i+1)%len(poly)];dx,dz=bx-ax,bz-az;length=math.hypot(dx,dz)
        tx,tz=dx/length,dz/length;nx,nz=tz,-tx;cx,cz=(ax+bx)/2,(az+bz)/2;yaw=math.atan2(tz,tx)
        for y,h,d,out,mat in [(top-.35,.28,.7,.12,'Facade_Limestone'),(top-.82,.42,.5,.06,'Facade_Limestone'),(top-1.22,.23,.32,.10,'Facade_Turquoise'),(top-1.45,.1,.4,.16,'Facade_Bronze')]:
            box(local(cx,cz,tx,tz,nx,nz,0,y,out),(length,h,d),mat,yaw)
        if top<10:continue
        base=max(1,top-10)
        for s in [-length*.34,length*.34]:
            h=top-1.6-base
            pos=local(cx,cz,tx,tz,nx,nz,s,base+h/2,.28)
            box(pos,(1.15,h,.7),'Facade_Limestone',yaw)
            box(local(cx,cz,tx,tz,nx,nz,s,top-1.95,.40),(1.65,.6,.95),'Facade_Limestone',yaw)
            box(local(cx,cz,tx,tz,nx,nz,s,base+.35,.40),(1.45,.7,.95),'Facade_Limestone',yaw)
            box(local(cx,cz,tx,tz,nx,nz,s,base+h*.5,.66),(.23,h*.72,.08),'Facade_Turquoise',yaw)
        if length>6:relief(cx,cz,tx,tz,nx,nz,base+1.3,1)
        # Dark recessed panels under terrace create portico rhythm without changing route surfaces.
        if length>9:
            for s in [-length*.18,length*.18]:
                box(local(cx,cz,tx,tz,nx,nz,s,base+3.4,.12),(1.55,5.8,.18),'Facade_Shadow',yaw)
                cylinder(local(cx,cz,tx,tz,nx,nz,s,base+3.1,.7),.28,5.6)

# Perimeter: dressed wall, battered pylons, decorated tablets and projecting cornices.
for side in range(4):
    angle=side*math.pi/2;tx,tz=math.cos(angle),math.sin(angle);nx,nz=tz,-tx;cx,cz=nx*70,nz*70;region='Perimeter_'+str(side)
    for j in range(-6,7):
        s=j*8.5
        # Center break preserves the axis; corners remain clear for the existing entrance stairs.
        if abs(s)<6:continue
        box(local(cx,cz,tx,tz,nx,nz,s,3.6,.15),(8.3,6.4,1.8),'Facade_Sandstone',angle)
        box(local(cx,cz,tx,tz,nx,nz,s,6.95,.45),(8.5,.5,2.5),'Facade_Limestone',angle)
        box(local(cx,cz,tx,tz,nx,nz,s,6.35,1.10),(8.5,.42,.17),'Facade_Turquoise',angle)
        box(local(cx,cz,tx,tz,nx,nz,s,6.03,1.16),(8.5,.15,.2),'Facade_Bronze',angle)
        # Tall relief on alternating blocks, flanked by broad piers.
        relief(cx+tx*s,cz+tz*s,tx,tz,nx,nz,1.1,.8,1.05)
        for ds in [-3.7,3.7]:
            box(local(cx,cz,tx,tz,nx,nz,s+ds,3.55,1.12),(.7,6.2,1.1),'Facade_Limestone',angle)

# Carved bands on stair buttress side faces, with the original tread and rail geometry untouched.
for p in parts:
    if not p['name'].startswith('TREADS_FLUSH') or 'ENTRY' in p['name']:continue
    pts=[vec(v) for v in p['vertices']];lo=min(v[1] for v in pts);hi=max(v[1] for v in pts)
    if hi-lo<3:continue
    low=[v for v in pts if v[1]<lo+.1];high=[v for v in pts if v[1]>hi-.1]
    a=Vector(tuple(sum(v[i] for v in low)/len(low) for i in range(3)))
    b=Vector(tuple(sum(v[i] for v in high)/len(high) for i in range(3)))
    direction=b-a;flat=math.hypot(direction.x,direction.z);tx,tz=direction.x/flat,direction.z/flat;nx,nz=tz,-tx
    width=max(abs((v[0]-a.x)*nx+(v[2]-a.z)*nz) for v in pts)
    region=p['name'].replace('TREADS_FLUSH','Buttress')
    for sign in [-1,1]:
        # Discrete carved uprights below stair flights.
        for j in range(1,int(flat/4)):
            t=j*4/flat;point=a+direction*t;h=min(5,point.y-1)
            if h<1:continue
            pos=(point.x+nx*sign*(width+.27),point.y-h/2-.25,point.z+nz*sign*(width+.27))
            box(pos,(.55,h,.55),'Facade_Limestone')
            box((pos[0],pos[1],pos[2]),(.62,.28,.62),'Facade_Turquoise')

# Broad structural courses envelop the old small non-playable terrace steps.
# Exact play surfaces cap the new masonry below the existing decks and treads.
region='Podium_structural_shell';step=1.0;extent=73;n=int(extent*2/step)
heights={}
caps={}
for face in pf:
    tri=[pv[i] for i in face]
    # Conservative footprint including a 0.8m shoulder, even at diagonal tread edges.
    x0=max(0,int(math.floor((min(v[0] for v in tri)-.8+extent)/step)))
    x1=min(n-1,int(math.floor((max(v[0] for v in tri)+.8+extent)/step)))
    z0=max(0,int(math.floor((min(v[2] for v in tri)-.8+extent)/step)))
    z1=min(n-1,int(math.floor((max(v[2] for v in tri)+.8+extent)/step)))
    limit=min(v[1] for v in tri)-.3
    for ix in range(x0,x1+1):
        for iz in range(z0,z1+1):caps[ix,iz]=min(caps.get((ix,iz),60),limit)
for ix in range(n):
    for iz in range(n):
        x=-extent+(ix+.5)*step;z=-extent+(iz+.5)*step
        hit=body_bvh.ray_cast(Vector((x,60,z)),Vector((0,-1,0)),62)
        if hit[0] is None or hit[0].y<1:continue
        old=hit[0].y
        # Four monumental storeys replace the old many small pyramid steps.
        # Rounding upward encloses source collision; route ceilings still take precedence.
        level=min(48,math.ceil(old/12)*12)
        # Deliberate architectural footprints, rather than quantizing tiny source wall fragments.
        for hx,hz,top in [(58,62,12),(40,48,24),(21,34,36),(12,20,46)]:
            if abs(x)<hx and abs(z)<hz:level=max(level,top)
        ceiling=60
        for dx,dz in [(-.49,-.49),(.49,-.49),(-.49,.49),(.49,.49),(0,0)]:
            ph=protect.ray_cast(Vector((x+dx,60,z+dz)),Vector((0,-1,0)),61)
            if ph[0] is not None:ceiling=min(ceiling,ph[0].y-.22)
        h=max(0,min(level,ceiling,caps.get((ix,iz),60)))
        heights[ix,iz]=h
gallery_edges={}
for (ix,iz),h in heights.items():
    x=-extent+ix*step;z=-extent+iz*step
    add([(x,h,z),(x+step,h,z),(x+step,h,z+step),(x,h,z+step)],[(0,3,2,1)],'Facade_Sandstone')
    for ox,oz,edge in [(-1,0,[(x,z+step),(x,z)]),(1,0,[(x+step,z),(x+step,z+step)]),(0,-1,[(x,z),(x+step,z)]),(0,1,[(x+step,z+step),(x,z+step)])]:
        low=heights.get((ix+ox,iz+oz),0)
        if low>=h:continue
        (ax,az),(bx,bz)=edge
        add([(ax,low,az),(bx,low,bz),(bx,h,bz),(ax,h,az)],[(0,1,2,3)],'Facade_Sandstone')
        if h-low>=5 and h in [12,24,36,46,48]:
            plane=ax if ox else az
            gallery_edges.setdefault((ox,oz,plane,h,round(low,2)),[]).append(min(az,bz) if ox else min(ax,bx))

# Architectural galleries follow long straight walls. Short stair-clearance notches receive no
# projecting cornices, so diagonal stairs do not acquire a serrated fringe of little roofs.
for (nx,nz,plane,h,low),coordinates in gallery_edges.items():
    runs=[]
    for q in sorted(coordinates):
        if not runs or q>runs[-1][-1]+step+.01:runs.append([q])
        else:runs[-1].append(q)
    for run in runs:
        length=run[-1]+step-run[0]
        if length<6:continue
        center=(run[0]+run[-1]+step)/2
        tx,tz=(0,1) if nx else (1,0)
        cx,cz=(plane,center) if nx else (center,plane)
        yaw=math.atan2(tz,tx);region='Gallery_tier_'+str(int(h))
        base=max(low,.8);height=h-base-1.3
        roof=box(local(cx,cz,tx,tz,nx,nz,0,h-.35,1.15),(length+.04,.65,3.5),'Facade_Limestone',yaw)
        if not roof:continue
        box(local(cx,cz,tx,tz,nx,nz,0,h-.85,2.88),(length,.3,.12),'Facade_Turquoise',yaw)
        box(local(cx,cz,tx,tz,nx,nz,0,h-1.05,2.96),(length,.09,.16),'Facade_Bronze',yaw)
        box(local(cx,cz,tx,tz,nx,nz,0,base+height*.5,.025),(length,height,.06),'Facade_Shadow',yaw)
        count=max(2,math.floor(length/5)+1)
        for j in range(count):
            s=-length/2+.85+(length-1.7)*j/(count-1)
            pos=local(cx,cz,tx,tz,nx,nz,s,base+height*.5,2.1)
            box(pos,(1.22,height,1.22),'Facade_Limestone',yaw)
            box(local(cx,cz,tx,tz,nx,nz,s,h-1,2.1),(1.75,.6,1.75),'Facade_Limestone',yaw)
            box(local(cx,cz,tx,tz,nx,nz,s,base+.22,2.1),(1.7,.44,1.7),'Facade_Limestone',yaw)
            box(local(cx,cz,tx,tz,nx,nz,s,base+.63,2.1),(1.28,.18,1.28),'Facade_Turquoise',yaw)
            if j%3==0 and height>5:
                px,pz=cx+tx*s,cz+tz*s
                relief(px,pz,tx,tz,nx,nz,base+1.5,.4,2.72)

objects=[]
for (region,mat),(verts,faces) in groups.items():
    if not faces:continue
    mesh=bpy.data.meshes.new(region+'_'+mat);mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new(region+'_'+mat,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(materials[mat]);objects.append(o)
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    if not region.startswith('Podium'):
        bevel=o.modifiers.new('Small worn arrises','BEVEL');bevel.width=.035;bevel.segments=1
        bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'Temple_Architecture_v1.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'Temple_Architecture_v1.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
report={'objects':len(objects),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),'rejected_route_overlaps':skipped,'protected_surface_triangles':len(pf),'platforms':sum(p['name'].endswith('_deck') for p in parts)}
(DOC/'blender-validation.json').write_text(json.dumps(report,indent=2));print('ARCHITECTURE_MODEL_COMPLETE',report)
# Keep a compact replay input, not the 188MB survey of unrelated bricks.
small=[p for p in parts if p['name'].endswith('_deck') or p['name'].startswith('TREADS_FLUSH')]
(DOC/'structure-reference.json').write_text(json.dumps({'parts':small},separators=(',',':')))
