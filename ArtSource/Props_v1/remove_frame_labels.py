import bpy, json, traceback
from pathlib import Path
p = Path(__file__).parent
out = p.parents[1] / 'Assets/Resources/ZeroHourAssets/Props_v1/Models'
try:
    bpy.ops.wm.open_mainfile(filepath=str(p / 'ZeroDuty_Props_v1.blend'))
    source = bpy.data.scenes['01_ASSETS_real_scale']
    frames = [c for c in source.collection.children if c.name.startswith('CAM_') and c.name.endswith('WallReference')]
    removed = []
    for col in frames:
        for obj in list(col.objects):
            if obj.type == 'FONT' and 'NORMAL STATE' in obj.data.body:
                removed.append(obj.data.body)
                bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(p / 'ZeroDuty_Props_v1.blend'))
    bpy.context.window.scene = source
    # Keep the existing Unity material names and FBX filenames for stable remapping.
    for i, mat in enumerate(list(bpy.data.materials)):
        if not mat.name.startswith('Studio'): mat.name = 'ZD_Prop_' + str(i).zfill(2)
    for col in frames:
        root = next(o for o in col.objects if o.type == 'EMPTY')
        root.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in col.objects:
            if obj.type in {'MESH','FONT'}: obj.select_set(True)
        bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
        bpy.ops.object.convert(target='MESH')
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = col.name
        obj.parent = None
        bpy.context.scene.cursor.location = (0,0,0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        obj.rotation_euler.x = 1.57079632679
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        bpy.ops.export_scene.fbx(filepath=str(out/f'{col.name}.fbx'), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_space_transform=True, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    (p/'frame_labels_removed.json').write_text(json.dumps({'removed':removed,'exported':[c.name for c in frames]},indent=2),encoding='utf-8')
except BaseException:
    (p/'frame_labels_error.txt').write_text(traceback.format_exc(),encoding='utf-8')
    raise
