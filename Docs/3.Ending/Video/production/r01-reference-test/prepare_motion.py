"""Export only approved R01 blocking, without editorial text or sound."""
import bpy
import json
from pathlib import Path

out = Path(__file__).resolve().parent
source = out.parent / 'previs49' / 'SandGuard-Ending49-Previs.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.context.scene
scene.camera = bpy.data.objects['R01 camera']
scene.frame_start = 1
scene.frame_end = 108
scene.render.fps = 24
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.use_sequencer = False
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'
(out / 'frames').mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(out / 'frames' / 'r01_')
(out / 'motion-manifest.json').write_text(json.dumps({
    'source_blend': str(source), 'camera': scene.camera.name,
    'source_frames': [1, 108], 'fps': 24,
    'action_seconds': 4.5, 'end_hold_seconds': 0.5,
    'resolution': [1280, 720],
    'changes': ['Disable editorial sequencer', 'No change to blocking, camera or geometry'],
    'test': 'Seedance 2.5 omni_reference, one shot only'
}, indent=2), encoding='utf-8')
bpy.ops.render.render(animation=True)
