"""Audit and separate every V6 stair/bridge rail; unwrap in a metric rail frame."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/DesertTowerLevels/TrimSheet'
DOC=ROOT/'Docs/LevelArt/TrimSheet'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/DesertTemple/Editor/DesertTemple_Visual.fbx'))
original=list(bpy.context.scene.objects)
rails=sorted([o for o in original if o.type=='MESH' and o.name.startswith('RAILS_REBUILT_')],key=lambda o:o.name)
assert len(rails)==22, f'Unexpected rail inventory: {len(rails)}'
image=bpy.data.images.load(str(OUT/'Textures/SandGuard_Sandstone_Trim_v1.png'))
mat=bpy.data.materials.new('SandGuard_Sandstone_Trim_v1'); mat.use_nodes=True
nodes=mat.node_tree.nodes; links=mat.node_tree.links
tex=nodes.new('ShaderNodeTexImage'); tex.image=image
uv=nodes.new('ShaderNodeTexCoord'); sep=nodes.new('ShaderNodeSeparateXYZ'); comb=nodes.new('ShaderNodeCombineXYZ'); mirror=nodes.new('ShaderNodeMath'); mirror.operation='PINGPONG'; mirror.inputs[1].default_value=1
links.new(uv.outputs['UV'],sep.inputs[0]); links.new(sep.outputs['X'],mirror.inputs[0]); links.new(mirror.outputs[0],comb.inputs['X']); links.new(sep.outputs['Y'],comb.inputs['Y']); links.new(comb.outputs[0],tex.inputs['Vector'])
bsdf=nodes.get('Principled BSDF'); links.new(tex.outputs['Color'],bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value=.86

def hull(points):
    pts=sorted(set((round(p[0],5),round(p[1],5)) for p in points))
    def cross(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    lower=[]; upper=[]
    for p in pts:
        while len(lower)>=2 and cross(lower[-2],lower[-1],p)<=.00001: lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper)>=2 and cross(upper[-2],upper[-1],p)<=.00001: upper.pop()
        upper.append(p)
    return [Vector(p) for p in lower[:-1]+upper[:-1]]

prepared=[]; audit=[]
for source in rails:
    bm=bmesh.new(); bm.from_mesh(source.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bm.verts.ensure_lookup_table(); seen=set(); groups=[]
    for v in bm.verts:
        if v in seen: continue
        stack=[v]; seen.add(v); group=[]
        while stack:
            a=stack.pop(); group.append(a)
            for e in a.link_edges:
                b=e.other_vert(a)
                if b not in seen: seen.add(b); stack.append(b)
        groups.append(group)
    assert len(groups)==2, (source.name,len(groups))
    along=max((e.verts[1].co-e.verts[0].co for e in bm.edges),key=lambda d:d.length).normalized()
    if (along.z if abs(along.z)>.01 else along[max(range(2),key=lambda i:abs(along[i]))])<0: along=-along
    across=Vector((-along.y,along.x,0)).normalized(); up=along.cross(across).normalized()
    groups.sort(key=lambda g:sum(v.co.dot(across) for v in g)/len(g),reverse=True)
    parent=bpy.data.objects.new(source.name,None); bpy.context.collection.objects.link(parent)
    record={'source':source.name,'componentsBefore':len(groups),'parts':[],'type':'bridge' if abs(along.z)<.01 else 'stairs'}
    for side,g in zip(['Left','Right'],groups):
        ids={v:i for i,v in enumerate(g)}; fs=[f for f in bm.faces if f.verts[0] in ids]
        points=[v.co.copy() for v in g]
        origin=sum(points,Vector())/len(points)
        basis=Matrix((along,across,up)).transposed().to_4x4(); basis.translation=origin
        local=[basis.inverted()@v for v in points]
        mesh=bpy.data.meshes.new(source.name+'__'+side); mesh.from_pydata(local,[],[[ids[v] for v in f.verts] for f in fs]); mesh.update()
        obj=bpy.data.objects.new(mesh.name,mesh); bpy.context.collection.objects.link(obj); obj.parent=parent; obj.matrix_world=source.matrix_world@basis
        mesh.materials.append(mat)
        for p in mesh.polygons: p.use_smooth=False
        profile=hull([(v.y,v.z) for v in local])
        # Cut only at the underside: the complete top/side profile stays connected.
        bottom=min(range(len(profile)),key=lambda i:(profile[i].y+profile[(i+1)%len(profile)].y)/2)
        chain=[profile[(bottom+1+j)%len(profile)] for j in range(len(profile))]
        distances=[0.0]
        for a,b in zip(chain,chain[1:]): distances.append(distances[-1]+(b-a).length)
        prepared.append((obj,chain,distances))
        error=max(((source.matrix_world@p)-(obj.matrix_world@q)).length for p,q in zip(points,local))
        assert error<.0001,(obj.name,error)
        record['parts'].append({'name':obj.name,'vertices':len(local),'profileLength':distances[-1],'positionError':error})
    audit.append(record); bm.free()

# One texel density for every face and every rail, including the long bridges.
metres_per_sheet=max(8,math.ceil(max(ds[-1] for _,_,ds in prepared)/.232))
for obj,chain,ds in prepared:
    mesh=obj.data; uv=mesh.uv_layers.new(name='Rail_Continuous_Trim')
    minx=min(v.co.x for v in mesh.vertices)
    miny=min(v.co.y for v in mesh.vertices); minz=min(v.co.z for v in mesh.vertices)
    def profile_distance(point):
        best=(float('inf'),0)
        for i,(a,b) in enumerate(zip(chain,chain[1:])):
            delta=b-a; t=max(0,min(1,(point-a).dot(delta)/delta.length_squared))
            error=(point-(a+t*delta)).length
            if error<best[0]: best=(error,ds[i]+t*delta.length)
        return best[1]
    for p in mesh.polygons:
        cap=abs(p.normal.x)>.6
        bottom=p.normal.z<-.9
        for li in p.loop_indices:
            c=mesh.vertices[mesh.loops[li].vertex_index].co
            if cap:
                u=.65+(c.y-miny)/metres_per_sheet
                v=.84+(c.z-minz)/metres_per_sheet
            elif bottom:
                u=(c.x-minx)/metres_per_sheet; v=.02+(c.y-miny)/metres_per_sheet
            else:
                u=(c.x-minx)/metres_per_sheet
                v=.758+profile_distance(Vector((c.y,c.z)))/metres_per_sheet
            uv.data[li].uv=(u,v)
    assert all(math.isfinite(x) for d in uv.data for x in d.uv)
    assert all(0<=d.uv.y<=.995 for d in uv.data),obj.name
    # Every shared longitudinal edge has identical UV coordinates on its two faces.
    continuity={}
    for p in mesh.polygons:
        if abs(p.normal.x)>.6 or p.normal.z<-.9: continue
        for li in p.loop_indices:
            vertex=mesh.loops[li].vertex_index; coord=uv.data[li].uv
            if vertex in continuity: assert (continuity[vertex]-coord).length<.00001
            continuity[vertex]=coord.copy()
    obj['source_group']=obj.parent.name; obj['uv_metres_per_sheet']=metres_per_sheet
    obj['uv_seam']='underside and end caps only'

# Source mirrored rails can have inward winding. Flip after UV assignment so
# existing per-corner UV coordinates are preserved along with all positions.
for obj,_,_ in prepared:
    check=bmesh.new(); check.from_mesh(obj.data)
    if check.calc_volume(signed=True)*obj.matrix_world.determinant()<0:
        bmesh.ops.reverse_faces(check,faces=list(check.faces))
        check.normal_update(); check.to_mesh(obj.data); obj.data.update()
    check.free()

for o in original: bpy.data.objects.remove(o,do_unlink=True)
image.pack()
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/DesertTemple_Rails_Split_v2.fbx'),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'SandGuard_Rails_Split_v2.blend'))
result={'groups':len(audit),'separateRails':len(prepared),'stairs':sum(x['type']=='stairs' for x in audit),'bridges':sum(x['type']=='bridge' for x in audit),'metresPerSheet':metres_per_sheet,'uvContinuity':'PASS: all shared top/side vertices; intentional seams on underside/end caps','inventory':audit}
(DOC/'rails-v2-validation.json').write_text(json.dumps(result,indent=2))
print('RAILS_SPLIT_PASS',json.dumps({k:v for k,v in result.items() if k!='inventory'}))
