import bpy, json, traceback
from pathlib import Path
p = Path(__file__).parent
out = p.parents[1] / 'Assets/Resources/ZeroHourAssets/Props_v1'
try:
    bpy.ops.wm.open_mainfile(filepath=str(p / 'ZeroDuty_Props_v1.blend'))
    bpy.context.window.scene = bpy.data.scenes['01_ASSETS_real_scale']
    (out / 'Models').mkdir(parents=True, exist_ok=True)
    materials = []
    for i, m in enumerate(list(bpy.data.materials)):
        if m.name.startswith('Studio'): continue
        old = m.name
        m.name = 'ZD_Prop_' + str(i).zfill(2)
        bs = m.node_tree.nodes.get('Principled BSDF') if m.use_nodes else None
        photo = next((n.image.name for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), '') if m.use_nodes else ''
        materials.append({'name': m.name, 'label': old, 'color': list(bs.inputs['Base Color'].default_value if bs else m.diffuse_color), 'metallic': bs.inputs['Metallic'].default_value if bs else 0, 'smoothness': 1-bs.inputs['Roughness'].default_value if bs else .5, 'emission': bs.inputs['Emission Strength'].default_value if bs else 0, 'photo': photo})
    names = []
    for col in list(bpy.context.scene.collection.children):
        roots = [o for o in col.objects if o.type == 'EMPTY']
        if not roots: continue
        name = col.name
        roots[0].location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT')
        for o in col.objects:
            if o.type in {'MESH','FONT'}: o.select_set(True)
        objects = list(bpy.context.selected_objects)
        if not objects: continue
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.convert(target='MESH')
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = name
        obj.parent = None
        bpy.context.scene.cursor.location = (0,0,0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        if name.startswith('CAM_'):
            obj.rotation_euler.x = 1.57079632679
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        bpy.ops.export_scene.fbx(filepath=str(out/'Models'/f'{name}.fbx'), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_space_transform=True, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
        names.append(name)
    (out/'import_manifest.json').write_text(json.dumps({'materials':materials, 'models':names},indent=2),encoding='utf-8')
    (p/'export_complete.json').write_text(json.dumps(names),encoding='utf-8')
except BaseException:
    (p/'export_error.txt').write_text(traceback.format_exc(),encoding='utf-8')
    raise
