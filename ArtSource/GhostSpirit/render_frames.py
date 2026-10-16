"""Render the supplied Blender ghost as transparent in-game sprite frames.

Run with Blender in background mode, passing the source .blend before --python.
The camera is frozen at the approved three-quarter view while the armature
continues animating, so every frame lines up on a Unity billboard.
"""

import os

import bpy


scene = bpy.context.scene
camera = scene.camera
scene.frame_set(50)
camera_pose = camera.evaluated_get(bpy.context.evaluated_depsgraph_get()).matrix_world.copy()
camera.parent = None
camera.animation_data_clear()
camera.constraints.clear()
camera.matrix_world = camera_pose
camera.data.lens = 70

scene.render.resolution_x = 768
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"

output_dir = "F:/Zero-duty/Assets/Resources/GhostSpirit"
os.makedirs(output_dir, exist_ok=True)
for index, frame in enumerate((40, 48, 56, 64, 72, 80)):
    scene.frame_set(frame)
    scene.render.filepath = f"{output_dir}/Frame_{index:02d}.png"
    bpy.ops.render.render(write_still=True)
