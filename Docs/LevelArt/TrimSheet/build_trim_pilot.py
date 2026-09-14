"""UV-author a bounded pilot on the actual V6 model; no raster processing."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/course/unity/SandGuard')
OUT=ROOT/'Assets/DesertTowerLevels/TrimSheet'
DOC=ROOT/'Docs/LevelArt/TrimSheet'
NAMES=['T1_CLEAN_MOLDINGS','T1_deck','T1_masonry',
       'B_NE_CLEAN_MOLDINGS','B_NE_deck','B_NE_masonry',
       'STAIR_FITTED_ENTRY_NE_T1','TREADS_FLUSH_ENTRY_NE_T1',
       'STAIR_FITTED_T1_B_NE','TREADS_FLUSH_T1_B_NE',
       'STAIR_FITTED_B_NE_T5','TREADS_FLUSH_B_NE_T5',
       'RAILS_REBUILT_ENTRY_NE_T1','RAILS_REBUILT_T1_B_NE','RAILS_REBUILT_B_NE_T5']
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/DesertTemple/Editor/DesertTemple_Visual.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
pilot=[bpy.data.objects[n] for n in NAMES]
image=bpy.data.images.load(str(OUT/'Textures/SandGuard_Sandstone_Trim_v1.png'))
assert image.size[0]==image.size[1] and image.size[0]>=1024, tuple(image.size)
mat=bpy.data.materials.new('SandGuard_Sandstone_Trim_v1'); mat.use_nodes=True
nodes=mat.node_tree.nodes; links=mat.node_tree.links
bsdf=nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value=.86
tex=nodes.new('ShaderNodeTexImage'); tex.image=image
uvnode=nodes.new('ShaderNodeTexCoord'); sep=nodes.new('ShaderNodeSeparateXYZ'); comb=nodes.new('ShaderNodeCombineXYZ')
mirror=nodes.new('ShaderNodeMath'); mirror.operation='PINGPONG'; mirror.inputs[1].default_value=1
links.new(uvnode.outputs['UV'],sep.inputs[0]); links.new(sep.outputs['X'],mirror.inputs[0]); links.new(mirror.outputs[0],comb.inputs['X']); links.new(sep.outputs['Y'],comb.inputs['Y']); links.new(comb.outputs[0],tex.inputs['Vector']); links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
report={'textureSize':list(image.size),'source':'Assets/DesertTemple/Editor/DesertTemple_Visual.fbx','objects':[],'notes':'UV only. Mirrored U repeat. No generated normal map; roughness 0.86. Positions and collision unchanged.'}

for o in pilot:
    before=[list(v) for v in o.bound_box]
    original_polys=len(o.data.polygons)
    bm=bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.dissolve_limit(bm,angle_limit=.0001,verts=list(bm.verts),edges=list(bm.edges),delimit={'NORMAL'})
    # Partition every walking surface at world-space V boundaries. This also keeps
    # long bridges and diagonal stair triangles from stretching or disagreeing.
    is_rail=o.name.startswith('RAILS_REBUILT_')
    if not o.name.endswith('_masonry') and not is_rail:
        for axis in [1]:
            low=min(v.co[axis] for v in bm.verts); high=max(v.co[axis] for v in bm.verts)
            for k in range(math.floor(low/2)+1, math.ceil(high/2)):
                co=Vector((0,0,0)); co[axis]=k*2; normal=Vector((0,0,0)); normal[axis]=1
                bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=co,plane_no=normal)
    bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()
    uv=o.data.uv_layers.new(name='TrimUV')
    mesh=o.data
    # Components give masonry blocks and stair slabs stable individual extents.
    parent=list(range(len(mesh.vertices)))
    def find(x):
        while parent[x]!=x: parent[x]=parent[parent[x]]; x=parent[x]
        return x
    for e in mesh.edges:
        a,b=e.vertices; parent[find(a)]=find(b)
    groups={}
    for v in mesh.vertices: groups.setdefault(find(v.index),[]).append(v.co)
    bounds={k:([min(v[i] for v in vs) for i in range(3)],[max(v[i] for v in vs) for i in range(3)]) for k,vs in groups.items()}
    rail_frames={}
    if is_rail:
        for k,vs in groups.items():
            edges=[mesh.vertices[e.vertices[1]].co-mesh.vertices[e.vertices[0]].co for e in mesh.edges if find(e.vertices[0])==k]
            along=max(edges,key=lambda d:d.x*d.x+d.y*d.y)
            tangent=Vector((along.x,along.y,0)).normalized()
            if (tangent.x if abs(tangent.x)>.01 else tangent.y)<0: tangent=-tangent
            slope=along.z/along.dot(tangent)
            across=Vector((-tangent.y,tangent.x,0))
            heights=[v.z-slope*v.dot(tangent) for v in vs]
            widths=[v.dot(across) for v in vs]
            rail_frames[k]=(tangent,across,slope,min(heights),max(heights),min(widths),max(widths))
    for p in mesh.polygons:
        n=p.normal; horizontal=abs(n.z)>.7
        points=[mesh.vertices[i].co for i in p.vertices]
        lo,hi=bounds[find(p.vertices[0])]
        if is_rail:
            # Follow each rail's actual slope, keeping mouldings parallel to its
            # top edge instead of stretching a world-height projection uphill.
            t,w,slope,hmin,hmax,wmin,wmax=rail_frames[find(p.vertices[0])]
            for li in p.loop_indices:
                c=mesh.vertices[mesh.loops[li].vertex_index].co
                length=c.dot(t)*math.sqrt(1+slope*slope)
                height=(c.z-slope*c.dot(t)-hmin)/max(hmax-hmin,.001)
                width=(c.dot(w)-wmin)/max(wmax-wmin,.001)
                if n.z>.5: u,v=length/8,.84+.10*width
                elif n.z<-.5: u,v=length/8,.02+.20*width
                elif abs(n.dot(t))>.65: u,v=.1+.1*width,.258+.234*height
                else: u,v=length/8,.508+.234*height
                uv.data[li].uv=(u,v)
            continue
        if o.name.endswith('_masonry'):
            # Sample the interior of one clean slab, avoiding painted joints on real blocks.
            axis=0 if abs(n.y)>=abs(n.x) else 1
            for li in p.loop_indices:
                c=mesh.vertices[mesh.loops[li].vertex_index].co
                u=.64+.26*(c[axis]-lo[axis])/max(hi[axis]-lo[axis],.001)
                v=.84+.10*(c[2]-lo[2])/max(hi[2]-lo[2],.001)
                uv.data[li].uv=(u,v)
            continue
        if horizontal:
            # One consistent projection for all coplanar faces in the same cell.
            cell=math.floor(sum(c.y for c in points)/len(points)/2)
            for li in p.loop_indices:
                c=mesh.vertices[mesh.loops[li].vertex_index].co
                uv.data[li].uv=(c.x/8, .758+.234*max(0,min(1,(c.y-cell*2)/2)))
        else:
            tangent=Vector((-n.y,n.x,0)).normalized()
            zmin=min(c.z for c in points); zmax=max(c.z for c in points)
            # Tall stair supports use weathered stone; platform fascia uses teal frieze.
            if 'MOLDINGS' in o.name: bottom,span=(.258,.234) if zmax-zmin>.12 else (.51,.23)
            elif zmax-zmin>1: bottom,span=.008,.234
            else: bottom,span=.508,.234
            for li in p.loop_indices:
                c=mesh.vertices[mesh.loops[li].vertex_index].co
                uv.data[li].uv=(c.dot(tangent)/8,bottom+span*(c.z-zmin)/max(zmax-zmin,.001))
    mesh.materials.clear(); mesh.materials.append(mat)
    for p in mesh.polygons: p.material_index=0
    assert max(abs(before[j][i]-o.bound_box[j][i]) for j in range(8) for i in range(3))<.0001
    assert all(math.isfinite(x) for d in uv.data for x in d.uv)
    report['objects'].append({'name':o.name,'sourcePolygons':original_polys,'polygons':len(mesh.polygons),'bounds':[list(o.matrix_world@Vector(c)) for c in o.bound_box]})

(OUT/'Models').mkdir(exist_ok=True,parents=True)
bpy.ops.object.select_all(action='DESELECT')
for o in pilot: o.select_set(True)
bpy.context.view_layer.objects.active=pilot[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/DesertTemple_TrimPilot_v1.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP',use_mesh_modifiers=True)
# Keep only pilot geometry in the editable source; the original FBX remains the context source.
for o in meshes:
    if o not in pilot: bpy.data.objects.remove(o,do_unlink=True)
image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'SandGuard_TrimPilot_v1.blend'))
(DOC/'blender-validation.json').write_text(json.dumps(report,indent=2))
print('TRIM_PILOT_VALIDATED',len(pilot),'objects',sum(x['polygons'] for x in report['objects']),'polygons')
