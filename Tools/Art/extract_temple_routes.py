"""Extract original Level walking surfaces, clipping only route boundaries.
Input comes from TempleCollisionAudit.Run; the original scene is read-only.
Run: blender --background --python Tools/Art/extract_temple_routes.py
"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'Docs/LevelArt/TempleArchitecture/V2'
AUDIT=ROOT/'Library/TempleCollisionAudit'
source=json.loads((AUDIT/'source-colliders.json').read_text())['parts'][0]
verts=[Vector(tuple(x[k] for k in ('x','y','z'))) for x in source['vertices']]
ids=source['triangles'];faces=[ids[j:j+3] for j in range(0,len(ids),3)]
bvh=BVHTree.FromPolygons(verts,faces,all_triangles=True)
surfaces=json.loads((AUDIT/'source-surfaces.json').read_text())['parts']
def hull(v):
 pts=sorted(set((round(x.x,5),round(x.z,5)) for x in v))
 def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
 a=[];b=[]
 for q in pts:
  while len(a)>1 and cross(a[-2],a[-1],q)<=1e-7:a.pop()
  a.append(q)
 for q in pts[::-1]:
  while len(b)>1 and cross(b[-2],b[-1],q)<=1e-7:b.pop()
  b.append(q)
 return a[:-1]+b[:-1]
def clip(poly,fn):
 out=[]
 for a,b in zip(poly,poly[1:]+poly[:1]):
  da,db=fn(a),fn(b)
  if da>=-1e-6:out.append(a)
  if (da>=0)!=(db>=0):out.append(a.lerp(b,da/(da-db)))
 return out
walk=[]
for f in faces:
 a,b,c=[verts[i] for i in f];n=(b-a).cross(c-a).normalized()
 walk.append(([a,b,c],n))
results=[]
for s in surfaces:
 if s['name'].startswith('RAILS'):continue
 vv=[Vector(tuple(x[k] for k in ('x','y','z'))) for x in s['vertices']]
 poly=hull(vv);lo=min(x.y for x in vv);hi=max(x.y for x in vv)
 center=sum(vv,Vector())/len(vv)
 hit,n,_,_=bvh.ray_cast(Vector((center.x,hi+1,center.z)),Vector((0,-1,0)),hi-lo+3)
 assert hit is not None,s['name']
 def height(x,z):return hit.y-(n.x*(x-hit.x)+n.z*(z-hit.z))/n.y
 # Original collision triangles are retained; clipping adds vertices only at the footprint boundary.
 tv=[];tf=[];samples=[]
 for tri,normal in walk:
  # Junction triangles include the adjoining ramp plane; retain those too.

  clipped=tri[:]
  for a,b in zip(poly,poly[1:]+poly[:1]):
   clipped=clip(clipped,lambda p:(b[0]-a[0])*(p.z-a[1])-(b[1]-a[1])*(p.x-a[0]))
   if len(clipped)<3:break
  clipped=clip(clipped,lambda p:height(p.x,p.z)+.35-p.y)
  clipped=clip(clipped,lambda p:p.y-height(p.x,p.z)+.35)
  if len(clipped)<3:continue
  for j in range(1,len(clipped)-1):
   a,b,c=clipped[0],clipped[j],clipped[j+1]
   if (b-a).cross(c-a).length<1e-8:continue
   k=len(tv);tv.extend((a,b,c));tf.extend((k,k+1,k+2))
 # Keep the source's actual boundary faces. Artificial slab end-caps would block
 # a capsule where diagonal ramps enter a platform below the deck height.
 upper=[Vector((x,height(x,z),z)) for x,z in poly]
 # Reference samples test interiors and near boundary against the original collision.
 cx=sum(x for x,z in poly)/len(poly);cz=sum(z for x,z in poly)/len(poly)
 for f in [.0,.4,.85,.98]:
  for x,z in poly:
   xx=cx+(x-cx)*f;zz=cz+(z-cz)*f
   h=bvh.ray_cast(Vector((xx,hi+1,zz)),Vector((0,-1,0)),hi-lo+3)[0]
   if h and abs(h.y-height(xx,zz))<.01:samples.append(dict(x=xx,y=h.y,z=zz))
 assert len(tf)>0 and samples,s['name']
 def d(v):return dict(x=v.x,y=v.y,z=v.z)
 results.append(dict(name=s['name'],vertices=list(map(d,tv)),triangles=tf,samples=samples,normal=d(n),outline=[d(v) for v in upper]))
print('EXTRACTED',len(results),'surfaces',sum(len(x['samples']) for x in results),'samples')
(DOC/'route-collision-reference.json').write_text(json.dumps(dict(parts=results),separators=(',',':')))


