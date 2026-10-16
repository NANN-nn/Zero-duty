import bpy, json
from pathlib import Path
p = Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath=str(p / 'ZeroDuty_Props_v1.blend'))
photos = [im for im in bpy.data.images if im.name.startswith('CAM_')]
assert len(photos) == 3
assert all(im.packed_file for im in photos)
assert '01_ASSETS_real_scale' in bpy.data.scenes
assert '02_PRESENTATION' in bpy.data.scenes
(p / 'validation.json').write_text(json.dumps({'reopened': True, 'packed_photos': [im.name for im in photos], 'scenes': list(bpy.data.scenes.keys())}, indent=2), encoding='utf-8')
