"""Bake seamless multiscale sandstone relief data with Blender Cycles (no lighting baked in)."""
import bpy, math
from pathlib import Path
root=Path('C:/course/unity/SandGuard')
out=root/'Assets/TempleArt/ArchitectureV2/StoneWeather.png'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_plane_add(size=2)
plane=bpy.context.object
mat=bpy.data.materials.new('Sandstone erosion bake');mat.use_nodes=True;plane.data.materials.append(mat)
n=mat.node_tree.nodes;l=mat.node_tree.links;n.clear()
uv=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');l.new(uv.outputs['UV'],sep.inputs[0])
def math_node(op,source,mult=None):
    node=n.new('ShaderNodeMath');node.operation=op;l.new(source,node.inputs[0])
    if mult is not None:node.inputs[1].default_value=mult
    return node.outputs[0]
u=math_node('MULTIPLY',sep.outputs['X'],math.tau);v=math_node('MULTIPLY',sep.outputs['Y'],math.tau)
co=n.new('ShaderNodeCombineXYZ');l.new(math_node('COSINE',u),co.inputs['X']);l.new(math_node('SINE',u),co.inputs['Y']);l.new(math_node('COSINE',v),co.inputs['Z'])
w=math_node('SINE',v)
combine=n.new('ShaderNodeCombineColor');combine.mode='RGB'
for channel,scale,detail,roughness in [('Red',2.7,5,.7),('Green',15,6,.8),('Blue',78,3,.75)]:
    noise=n.new('ShaderNodeTexNoise');noise.noise_dimensions='4D';noise.inputs['Scale'].default_value=scale;noise.inputs['Detail'].default_value=detail;noise.inputs['Roughness'].default_value=roughness
    l.new(co.outputs[0],noise.inputs['Vector']);l.new(w,noise.inputs['W']);l.new(noise.outputs['Fac'],combine.inputs[channel])
emission=n.new('ShaderNodeEmission');l.new(combine.outputs[0],emission.inputs[0]);output=n.new('ShaderNodeOutputMaterial');l.new(emission.outputs[0],output.inputs['Surface'])
image=bpy.data.images.new('StoneWeather',2048,2048,alpha=False);image.colorspace_settings.name='Non-Color'
target=n.new('ShaderNodeTexImage');target.image=image;n.active=target
bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=1;bpy.context.scene.cycles.device='CPU';bpy.context.scene.render.bake.margin=0
bpy.ops.object.bake(type='EMIT')
image.filepath_raw=str(out);image.file_format='PNG';image.save()
doc=root/'Docs/LevelArt/TempleArchitecture/V2/StoneBake.blend';bpy.ops.wm.save_as_mainfile(filepath=str(doc))
print('STONE_BAKE_COMPLETE',out)
