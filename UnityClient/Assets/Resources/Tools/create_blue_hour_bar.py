"""Generate the BLUE HOUR old-street bar facade and neon sign.

Run from the repository root:

    blender --background --python \
      UnityClient/Assets/Resources/Tools/create_blue_hour_bar.py

The script writes:
    Assets/Resources/Models/Buildings/blue_hour_bar.blend
    Assets/Resources/Models/Buildings/blue_hour_bar.fbx
    Assets/Resources/Models/Buildings/blue_hour_bar_preview.png

The faulting neon segment and spark origin are deliberately separate objects:
    Neon_Fault_R_Leg
    SparkSocket
"""

from __future__ import annotations

import math
import os
import random
from pathlib import Path

import bpy
from mathutils import Vector


SCRIPT_PATH = Path(__file__).resolve()
ASSETS_ROOT = SCRIPT_PATH.parents[2]
OUTPUT_DIR = ASSETS_ROOT / "Resources" / "Models" / "Buildings"
BLEND_PATH = OUTPUT_DIR / "blue_hour_bar.blend"
FBX_PATH = OUTPUT_DIR / "blue_hour_bar.fbx"
PREVIEW_PATH = OUTPUT_DIR / "blue_hour_bar_preview.png"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.curves,
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def collection(name: str) -> bpy.types.Collection:
    value = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(value)
    return value


def move_to_collection(obj: bpy.types.Object, target: bpy.types.Collection) -> None:
    for source in list(obj.users_collection):
        source.objects.unlink(obj)
    target.objects.link(obj)


def material_principled(
    name: str,
    base_color: tuple[float, float, float, float],
    *,
    metallic: float = 0.0,
    roughness: float = 0.5,
    emission_color: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = base_color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission_color is not None:
        bsdf.inputs["Emission Color"].default_value = emission_color
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def cube(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    mat: bpy.types.Material,
    target: bpy.types.Collection,
    *,
    bevel: float = 0.0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Soft worn edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
    obj.data.materials.append(mat)
    move_to_collection(obj, target)
    return obj


def curve_tube(
    name: str,
    points: list[tuple[float, float, float]],
    radius: float,
    mat: bpy.types.Material,
    target: bpy.types.Collection,
    *,
    cyclic: bool = False,
    bevel_resolution: int = 3,
) -> bpy.types.Object:
    data = bpy.data.curves.new(name, type="CURVE")
    data.dimensions = "3D"
    data.resolution_u = 2
    data.bevel_depth = radius
    data.bevel_resolution = bevel_resolution
    data.resolution_u = 12
    spline = data.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, data)
    target.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def poly_tube(
    name: str,
    points: list[tuple[float, float, float]],
    radius: float,
    mat: bpy.types.Material,
    target: bpy.types.Collection,
    *,
    cyclic: bool = False,
    bevel_resolution: int = 2,
) -> bpy.types.Object:
    data = bpy.data.curves.new(name, type="CURVE")
    data.dimensions = "3D"
    data.bevel_depth = radius
    data.bevel_resolution = bevel_resolution
    spline = data.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, coordinate in zip(spline.points, points):
        point.co = (*coordinate, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, data)
    target.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def build_scene() -> None:
    clear_scene()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    building = collection("BUILDING_DARK_VOLUMES")
    architecture_lines = collection("ARCHITECTURE_LINES")
    neon_stable = collection("NEON_STABLE")
    neon_fault = collection("NEON_FAULT")
    fx_markers = collection("FX_MARKERS")
    preview_collection = collection("PREVIEW_ONLY")

    dark_model = material_principled(
        "M_Dark_Blue_Model",
        (0.0025, 0.0065, 0.017, 1.0),
        roughness=0.94,
    )
    deep_recess = material_principled(
        "M_Deep_Recess",
        (0.0003, 0.0008, 0.0022, 1.0),
        roughness=1.0,
    )
    line_white = material_principled(
        "M_White_Emission_Lines",
        (0.84, 0.88, 0.92, 1.0),
        roughness=0.72,
        emission_color=(0.72, 0.78, 0.86, 1.0),
        emission_strength=1.65,
    )
    neon_blue = material_principled(
        "M_Neon_Blue",
        (0.50, 0.72, 1.0, 1.0),
        roughness=0.28,
        emission_color=(0.055, 0.30, 1.0, 1.0),
        emission_strength=10.0,
    )
    neon_fault_mat = material_principled(
        "M_Neon_Fault",
        (0.72, 0.84, 1.0, 1.0),
        roughness=0.26,
        emission_color=(0.20, 0.52, 1.0, 1.0),
        emission_strength=7.0,
    )

    front_y = -0.63
    line_y = -0.672
    side_y = 1.35
    line_radius = 0.012

    # The building is a nearly black volume. Its readable form comes from the
    # authored lines below, not from surface detail or realistic illumination.
    cube("Bar_Dark_Mass", (0.0, 0.36, 3.35), (5.6, 0.99, 3.35), dark_model, building)
    cube("Door_Recess", (0.0, front_y - 0.025, 2.08), (1.34, 0.025, 1.95), deep_recess, building)
    for name, x in (("Left", -3.75), ("Right", 3.75)):
        cube(
            f"Window_{name}_Recess",
            (x, front_y - 0.028, 2.20),
            (1.22, 0.022, 1.42),
            deep_recess,
            building,
        )

    def sketch_points(
        name: str,
        points: list[tuple[float, float, float]],
        cyclic: bool,
    ) -> list[tuple[float, float, float]]:
        """Add a restrained, deterministic hand-drawn drift to architecture lines."""
        rng = random.Random(f"BLUE_HOUR|{name}")
        result: list[tuple[float, float, float]] = []
        segment_count = len(points) if cyclic else len(points) - 1

        for segment_index in range(segment_count):
            start = Vector(points[segment_index])
            end = Vector(points[(segment_index + 1) % len(points)])
            direction = end - start
            if direction.length <= 0.0001:
                continue

            perpendicular = direction.normalized().cross(Vector((0.0, 1.0, 0.0)))
            if perpendicular.length <= 0.0001:
                perpendicular = direction.normalized().cross(Vector((1.0, 0.0, 0.0)))
            perpendicular.normalize()

            subdivisions = max(2, min(7, int(direction.length / 0.8) + 1))
            phase = rng.uniform(0.0, math.tau)
            amplitude = rng.uniform(0.004, 0.010)

            for index in range(subdivisions):
                t = index / subdivisions
                point = start.lerp(end, t)
                if index != 0:
                    envelope = math.sin(math.pi * t)
                    drift = math.sin(t * math.tau + phase) * amplitude * envelope
                    point += perpendicular * drift
                result.append(tuple(point))

        if not cyclic:
            result.append(points[-1])
        return result

    def line(
        name: str,
        points: list[tuple[float, float, float]],
        *,
        radius: float = line_radius,
        cyclic: bool = False,
    ) -> bpy.types.Object:
        obj = poly_tube(
            name,
            sketch_points(name, points, cyclic),
            radius,
            line_white,
            architecture_lines,
            cyclic=cyclic,
            bevel_resolution=2,
        )
        obj["ssnoir_line_role"] = "architecture"
        return obj

    def front_line(
        name: str,
        points: list[tuple[float, float]],
        *,
        radius: float = line_radius,
        cyclic: bool = False,
    ) -> bpy.types.Object:
        return line(
            name,
            [(x, line_y, z) for x, z in points],
            radius=radius,
            cyclic=cyclic,
        )

    def front_rect(
        name: str,
        x0: float,
        x1: float,
        z0: float,
        z1: float,
        *,
        radius: float = line_radius,
    ) -> bpy.types.Object:
        return front_line(
            name,
            [(x0, z0), (x1, z0), (x1, z1), (x0, z1)],
            radius=radius,
            cyclic=True,
        )

    # Only silhouette and structural edges: no bricks, stains, drainpipes or
    # material trim. The doubled cornice is the facade's one decorative gesture.
    front_rect("Outline_Facade", -5.6, 5.6, 0.0, 6.70, radius=0.016)
    front_line("Outline_Cornice_Upper", [(-5.6, 6.48), (5.6, 6.48)], radius=0.014)
    front_line("Outline_Cornice_Lower", [(-5.6, 4.24), (5.6, 4.24)], radius=0.014)
    front_line("Outline_Sill", [(-5.6, 0.38), (5.6, 0.38)])

    # A few side edges preserve the object as a building rather than a flat sign.
    line(
        "Outline_Right_TopDepth",
        [(5.6, line_y, 6.70), (5.6, side_y, 6.70)],
        radius=0.016,
    )
    line(
        "Outline_Right_Back",
        [(5.6, side_y, 6.70), (5.6, side_y, 0.0)],
        radius=0.016,
    )
    line(
        "Outline_Right_BaseDepth",
        [(5.6, side_y, 0.0), (5.6, line_y, 0.0)],
        radius=0.016,
    )
    line(
        "Outline_Right_CorniceDepth",
        [(5.6, line_y, 4.24), (5.6, side_y, 4.24)],
    )

    # Sparse window geometry. The inner line floats slightly away from the outer
    # frame, matching the hand-authored diagram quality in the project references.
    for name, x0, x1 in (
        ("Left", -4.95, -2.55),
        ("Right", 2.55, 4.95),
    ):
        front_rect(f"Outline_Window_{name}_Outer", x0, x1, 0.72, 3.60)
        front_rect(f"Outline_Window_{name}_Inner", x0 + 0.16, x1 - 0.16, 0.88, 3.44, radius=0.009)
        front_line(
            f"Outline_Window_{name}_Mullion",
            [(x0 + 0.16, 2.16), (x1 - 0.16, 2.16)],
            radius=0.009,
        )

    # The entrance is a single angular Art Deco gesture. It reads as a doorway
    # through its lines and black void, without doors rendered as colored panels.
    front_line(
        "Outline_Door_Outer",
        [
            (-1.36, 0.18),
            (-1.36, 3.56),
            (-0.92, 4.02),
            (0.92, 4.02),
            (1.36, 3.56),
            (1.36, 0.18),
        ],
        radius=0.017,
    )
    front_line("Outline_Door_Center", [(0.0, 0.18), (0.0, 3.86)], radius=0.010)
    front_line("Outline_Door_Transom", [(-1.18, 3.42), (1.18, 3.42)], radius=0.010)
    front_line("Outline_Door_LeftInset", [(-1.12, 0.52), (-1.12, 3.30), (-0.15, 3.30)], radius=0.008)
    front_line("Outline_Door_RightInset", [(1.12, 0.52), (1.12, 3.30), (0.15, 3.30)], radius=0.008)
    front_line("Outline_Handle_Left", [(-0.28, 1.72), (-0.28, 2.08)], radius=0.018)
    front_line("Outline_Handle_Right", [(0.28, 1.72), (0.28, 2.08)], radius=0.018)

    # Bespoke cursive centerlines. These are not font outlines: every path below
    # is the center of one bent glass tube. Capitals are split where a real sign
    # maker would need separate tube runs; the lowercase runs remain connected.
    def neon_path(
        name: str,
        points: list[tuple[float, float]],
        *,
        fault: bool = False,
        radius: float = 0.020,
    ) -> bpy.types.Object:
        obj = curve_tube(
            name,
            [(x, -0.72, z) for x, z in points],
            radius,
            neon_fault_mat if fault else neon_blue,
            neon_fault if fault else neon_stable,
            bevel_resolution=4,
        )
        obj["ssnoir_neon_role"] = "fault" if fault else "stable"
        obj["ssnoir_emission_property"] = "_EmissionColor"
        return obj

    # Blue: a tall looped B followed by one continuous l-u-e run.
    neon_path(
        "Neon_Stable_Blue_B_Stem",
        [(-3.18, 4.91), (-3.13, 5.35), (-3.05, 5.82), (-2.90, 5.91)],
    )
    neon_path(
        "Neon_Stable_Blue_B_Lobes",
        [
            (-3.08, 5.79),
            (-2.72, 5.95),
            (-2.39, 5.80),
            (-2.45, 5.57),
            (-2.80, 5.43),
            (-3.09, 5.45),
            (-2.72, 5.43),
            (-2.34, 5.24),
            (-2.42, 4.99),
            (-2.79, 4.88),
            (-3.17, 5.00),
        ],
    )
    neon_path(
        "Neon_Stable_Blue_lue",
        [
            (-2.35, 5.04),
            (-2.18, 5.09),
            (-1.98, 5.82),
            (-1.82, 5.91),
            (-1.83, 5.50),
            (-1.99, 5.10),
            (-1.72, 5.01),
            (-1.50, 5.36),
            (-1.55, 5.08),
            (-1.30, 5.00),
            (-1.08, 5.34),
            (-0.79, 5.32),
            (-0.91, 5.14),
            (-1.20, 5.16),
            (-0.84, 4.99),
            (-0.46, 5.09),
        ],
    )

    # Hour: an open, calligraphic H and a continuous o-u-r connection.
    neon_path(
        "Neon_Stable_Hour_H_Left",
        [(0.02, 4.90), (0.18, 5.38), (0.34, 5.88), (0.49, 5.94)],
    )
    neon_path(
        "Neon_Stable_Hour_H_Right",
        [(0.86, 4.91), (1.03, 5.40), (1.18, 5.86), (1.35, 5.91)],
    )
    neon_path(
        "Neon_Stable_Hour_H_Crossbar",
        [(0.12, 5.38), (0.52, 5.30), (0.93, 5.34), (1.31, 5.43)],
        radius=0.017,
    )
    neon_path(
        "Neon_Stable_Hour_our",
        [
            (1.23, 5.09),
            (1.46, 5.07),
            (1.57, 5.34),
            (1.82, 5.38),
            (1.99, 5.20),
            (1.87, 5.02),
            (1.57, 5.05),
            (1.98, 5.13),
            (2.20, 5.35),
            (2.14, 5.06),
            (2.40, 4.99),
            (2.60, 5.31),
            (2.57, 5.05),
            (2.82, 5.12),
            (2.96, 5.37),
            (3.13, 5.35),
        ],
    )
    fault_object = neon_path(
        "Neon_Fault_R_Leg",
        [(3.10, 5.35), (3.18, 5.11), (3.43, 5.00), (3.72, 5.10)],
        fault=True,
    )

    # A thin baseline binds the two words into a single sign and rises into the
    # terminal flourish without becoming a second outline around the letters.
    neon_path(
        "Neon_Stable_Swoop",
        [
            (-3.45, 4.82),
            (-2.15, 4.75),
            (-0.55, 4.78),
            (1.10, 4.75),
            (2.70, 4.78),
            (3.58, 4.91),
        ],
        radius=0.012,
    )

    fault_endpoint = Vector((3.0, -0.74, 5.10))
    if fault_object is not None:
        fault_endpoint = fault_object.matrix_world @ fault_object.data.splines[0].bezier_points[-1].co

    bpy.ops.object.empty_add(type="SPHERE", radius=0.07, location=fault_endpoint)
    spark_socket = bpy.context.object
    spark_socket.name = "SparkSocket"
    spark_socket["ssnoir_fx_role"] = "neon_spark_origin"
    move_to_collection(spark_socket, fx_markers)
    if fault_object is not None:
        fault_object["ssnoir_spark_socket"] = "SparkSocket"

    # The pavement is another dark mass with only its perimeter drawn.
    ground = cube(
        "Preview_Ground",
        (0.0, -0.20, -0.10),
        (7.25, 3.10, 0.10),
        dark_model,
        preview_collection,
    )
    ground["ssnoir_preview_only"] = True
    pavement_line = poly_tube(
        "Preview_Pavement_Outline",
        [
            (-7.25, -3.30, 0.01),
            (7.25, -3.30, 0.01),
            (7.25, 2.90, 0.01),
            (-7.25, 2.90, 0.01),
        ],
        0.012,
        line_white,
        preview_collection,
        cyclic=True,
    )
    pavement_line["ssnoir_preview_only"] = True

    # Very weak fill only separates the mass from pure black. It does not model
    # realistic lighting; the emitted lines remain the visual source of truth.
    fill_data = bpy.data.lights.new("Preview_Mass_Fill", type="AREA")
    fill_data.energy = 32.0
    fill_data.color = (0.08, 0.12, 0.24)
    fill_data.shape = "DISK"
    fill_data.size = 8.0
    fill = bpy.data.objects.new("Preview_Mass_Fill", fill_data)
    preview_collection.objects.link(fill)
    fill.location = (-3.5, -5.5, 7.5)
    fill.rotation_euler = (math.radians(54), 0.0, math.radians(-28))
    fill["ssnoir_preview_only"] = True

    camera_data = bpy.data.cameras.new("Preview_Camera")
    camera = bpy.data.objects.new("Preview_Camera", camera_data)
    preview_collection.objects.link(camera)
    camera.location = (9.6, -22.0, 7.80)
    camera_data.lens = 58.0
    camera_data.sensor_width = 36.0
    camera["ssnoir_preview_only"] = True

    target = Vector((0.0, 0.15, 3.15))
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera

    world = bpy.data.worlds.new("Blue Hour World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.00015, 0.00035, 0.0012, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.025

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Very High Contrast"

    # Compositor glare makes the preview resemble the URP Bloom target.
    compositor = bpy.data.node_groups.new("BLUE_HOUR_Compositor", "CompositorNodeTree")
    scene.compositing_node_group = compositor
    nodes = compositor.nodes
    links = compositor.links
    nodes.clear()
    render_layers = nodes.new("CompositorNodeRLayers")
    glare = nodes.new("CompositorNodeGlare")
    glare.inputs["Type"].default_value = "Fog Glow"
    glare.inputs["Quality"].default_value = "High"
    glare.inputs["Threshold"].default_value = 0.72
    glare.inputs["Strength"].default_value = 0.72
    glare.inputs["Size"].default_value = 0.66
    compositor.interface.new_socket(
        name="Image",
        in_out="OUTPUT",
        socket_type="NodeSocketColor",
    )
    composite = nodes.new("NodeGroupOutput")
    links.new(render_layers.outputs["Image"], glare.inputs["Image"])
    links.new(glare.outputs["Image"], composite.inputs["Image"])

    scene["ssnoir_asset"] = "BLUE HOUR line-art old street bar"
    scene["ssnoir_fault_renderer"] = "Neon_Fault_R_Leg"
    scene["ssnoir_spark_socket"] = "SparkSocket"


def save_and_export() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    # Exclude preview-only objects from FBX. Collections remain named in the .blend,
    # while object names and custom properties survive the FBX handoff to Unity.
    bpy.ops.object.select_all(action="DESELECT")
    for group_name in (
        "BUILDING_DARK_VOLUMES",
        "ARCHITECTURE_LINES",
        "NEON_STABLE",
        "NEON_FAULT",
        "FX_MARKERS",
    ):
        group = bpy.data.collections.get(group_name)
        if group is None:
            continue
        for obj in group.all_objects:
            obj.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        use_custom_props=True,
    )

    bpy.context.scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))


if __name__ == "__main__":
    os.chdir(SCRIPT_PATH.parents[4])
    build_scene()
    save_and_export()
    print(f"BLUE HOUR bar generated: {BLEND_PATH}")
    print(f"Unity FBX generated: {FBX_PATH}")
    print(f"Preview rendered: {PREVIEW_PATH}")
