"""Frame an SSNoir asset with an orthographic 3/4 camera and render a preview.

Run with:
  blender --background asset.blend --python frame_and_render_preview.py -- \
    --output /absolute/preview.png [--camera-name Camera_Asset] [--save-camera]
"""

import argparse
import json
import os
import sys

import bpy
from mathutils import Vector


def parse_args():
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--camera-name", default="Camera_Preview")
    parser.add_argument("--width", type=int, default=1200)
    parser.add_argument("--height", type=int, default=1200)
    parser.add_argument("--margin", type=float, default=1.18)
    parser.add_argument("--save-camera", action="store_true")
    return parser.parse_args(args)


options = parse_args()
if not os.path.isabs(options.output):
    raise SystemExit("--output must be an absolute path")
if options.width <= 0 or options.height <= 0:
    raise SystemExit("preview dimensions must be positive")
if options.margin <= 1.0:
    raise SystemExit("--margin must be greater than 1")

scene = bpy.context.scene
renderables = [
    obj for obj in scene.objects if obj.type in {"MESH", "CURVE"} and not obj.hide_render
]
if not renderables:
    raise SystemExit("scene contains no renderable mesh or curve")

world_corners = []
for obj in renderables:
    if obj.type == "MESH":
        world_corners.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    elif obj.type == "CURVE":
        for spline in obj.data.splines:
            world_corners.extend(
                obj.matrix_world @ Vector(point.co[:3]) for point in spline.points
            )
            world_corners.extend(
                obj.matrix_world @ point.co for point in spline.bezier_points
            )

if not world_corners:
    world_corners = [
        obj.matrix_world @ Vector(corner)
        for obj in renderables
        for corner in obj.bound_box
    ]
minimum = Vector(
    tuple(min(point[axis] for point in world_corners) for axis in range(3))
)
maximum = Vector(
    tuple(max(point[axis] for point in world_corners) for axis in range(3))
)
center = (minimum + maximum) * 0.5
extent = maximum - minimum

camera = bpy.data.objects.get(options.camera_name)
if camera is not None and camera.type != "CAMERA":
    raise SystemExit(f"'{options.camera_name}' exists but is not a camera")
if camera is None:
    camera_data = bpy.data.cameras.new(options.camera_name)
    camera = bpy.data.objects.new(options.camera_name, camera_data)
    scene.collection.objects.link(camera)

direction = Vector((1.0, -1.15, 0.9)).normalized()
distance = max(extent.length * 2.0, 1.0)
camera.location = center + direction * distance
camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
scene.camera = camera

inverse_rotation = camera.rotation_euler.to_matrix().inverted()
camera_space = [inverse_rotation @ (point - center) for point in world_corners]
projected_width = max(point.x for point in camera_space) - min(
    point.x for point in camera_space
)
projected_height = max(point.y for point in camera_space) - min(
    point.y for point in camera_space
)
aspect = options.width / options.height
camera.data.ortho_scale = max(projected_height, projected_width / aspect) * options.margin

scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = options.width
scene.render.resolution_y = options.height
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.filepath = options.output

scene.display.shading.light = "STUDIO"
scene.display.shading.studio_light = "paint.sl"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "WORLD"
scene.display.shading.show_specular_highlight = True
scene.display.shading.background_type = "WORLD"
scene.display.shading.background_color = (0.035, 0.035, 0.035)

os.makedirs(os.path.dirname(options.output), exist_ok=True)
if options.save_camera:
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
bpy.ops.render.render(write_still=True)

print(
    "SSNOIR_PREVIEW "
    + json.dumps(
        {
            "file": bpy.data.filepath,
            "output": options.output,
            "camera": camera.name,
            "location": [round(value, 4) for value in camera.location],
            "ortho_scale": round(camera.data.ortho_scale, 4),
            "saved_camera": options.save_camera,
        },
        ensure_ascii=False,
    )
)
