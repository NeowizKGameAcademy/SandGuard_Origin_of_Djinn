import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/course/unity/SandGuard')
OUT=ROOT/'Assets/Enemy/Art/ChiefBomb'
DOC=ROOT/'Docs/model-art/chief-bomb'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def material(name,color,metal=0,rough=.65):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    return m
iron=material('Bomb_BlackIron',(.07,.085,.095),.55,.5)
gold=material('Bomb_AgedBrass',(.55,.32,.095),.65,.42)
red=material('Bomb_Crimson',(.28,.035,.025),.05,.85)
rope=material('Bomb_FuseRope',(.50,.36,.19),0,.95)
ember=material('Bomb_Ember',(.95,.19,.015),0,.6)
parts=[]
def finish(o,name,m):
    o.name=name; o.data.materials.append(m); parts.append(o); return o
def ring(name,z,major,minor,m):
    bpy.ops.mesh.primitive_torus_add(major_segments=16,minor_segments=4,location=(0,0,z),major_radius=major,minor_radius=minor)
    return finish(bpy.context.object,name,m)
def cylinder(name,a,b,radius,m,vertices=8):
    a,b=Vector(a),Vector(b); d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=d.length,location=(a+b)/2)
    o=bpy.context.object; o.rotation_euler=d.to_track_quat('Z','Y').to_euler(); return finish(o,name,m)
bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.185)
body=finish(bpy.context.object,'IronShell',iron); body.scale.z=.92
# Two restrained gold edges frame the chief's red waist band.
cylinder('CrimsonBand',(0,0,-.018),(0,0,.018),.186,red,16)
ring('BrassBandUpper',.023,.184,.008,gold); ring('BrassBandLower',-.023,.184,.008,gold)
cylinder('FuseSocket',(0,0,.145),(0,0,.19),.046,gold,12)
cylinder('SocketInner',(0,0,.19),(0,0,.195),.027,iron,12)
for i in range(8):
    angle=2*math.pi*i/8
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.009,location=(.189*math.cos(angle),.189*math.sin(angle),0))
    finish(bpy.context.object,'BandRivet_%02d'%i,gold)
# Small front diamond plate: geometric decoration, no decal/texture dependency.
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,-.183,.065))
badge=finish(bpy.context.object,'ChiefDiamond',gold); badge.scale=(.040,.008,.040); badge.rotation_euler.y=math.pi/4
path=[(0,0,.193),(.006,0,.219),(.021,0,.245),(.043,0,.263),(.065,0,.269)]
for i,(a,b) in enumerate(zip(path,path[1:])): cylinder('Fuse_%02d'%i,a,b,.010,rope)
for i in range(7):
    t=(i+.5)/7*(len(path)-1); k=min(int(t),len(path)-2); f=t-k
    p=Vector(path[k]).lerp(Vector(path[k+1]),f); tangent=Vector(path[k+1])-Vector(path[k])
    bpy.ops.mesh.primitive_torus_add(major_segments=8,minor_segments=4,location=p,major_radius=.010,minor_radius=.0018)
    o=finish(bpy.context.object,'FuseBraid_%02d'%i,gold); o.rotation_euler=tangent.to_track_quat('Z','Y').to_euler()
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.012,location=path[-1]); tip=finish(bpy.context.object,'FuseEmber',ember)
# One render mesh with material slots; emissive tip is a separate selectable piece.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
    if o!=tip:o.select_set(True)
bpy.context.view_layer.objects.active=body; bpy.ops.object.join(); body.name='ChiefBomb_Mesh'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
body.data.update()
for o in [body,tip]:
    for p in o.data.polygons:p.use_smooth=False
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'ChiefBomb.fbx'),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'ChiefBomb.blend'))
report={'bodyDiameter':.386,'heightWithFuse':.452,'triangles':sum(len(p.vertices)-2 for o in [body,tip] for p in o.data.polygons),'fuseTipBlender':path[-1],'materials':[m.name for m in body.data.materials]}
(DOC/'model-validation.json').write_text(json.dumps(report,indent=2)); print('CHIEF_BOMB_MODEL',json.dumps(report))
