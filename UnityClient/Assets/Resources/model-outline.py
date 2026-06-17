import bpy
import hashlib
import math
import re
from collections import defaultdict
from mathutils import Vector

ANGLE_THRESHOLD_DEGREES = 50
LINE_RADIUS = 0.002
MIN_EDGE_LENGTH = 0.02
OUTLINE_NORMAL_OFFSET = 0.003

LINE_OBJECT_NAME_PREFIX = "OutlineLines"
LINE_MATERIAL_NAME = "M_White_Emission_Lines"

MODEL_MATERIAL_NAME = "M_Dark_Blue_Model"
MODEL_COLOR_HEX = "0A142A"

def srgb_channel_to_linear(value):
    value = value / 255

    if value <= 0.04045:
        return value / 12.92

    return ((value + 0.055) / 1.055) ** 2.4

def hex_to_linear_rgba(hex_color):
    hex_color = hex_color.lstrip("#")

    if len(hex_color) != 6:
        raise Exception("Expected 6-digit hex color.")

    r = int(hex_color[0:2], 16)
    g = int(hex_color[2:4], 16)
    b = int(hex_color[4:6], 16)

    return (
        srgb_channel_to_linear(r),
        srgb_channel_to_linear(g),
        srgb_channel_to_linear(b),
        1
    )

def get_scene_meshes():
    selected_meshes = [
        obj for obj in bpy.context.selected_objects
        if obj.type == "MESH"
    ]

    if selected_meshes:
        return selected_meshes, "selected"

    scene_meshes = [
        obj for obj in bpy.context.scene.objects
        if obj.type == "MESH"
    ]

    return scene_meshes, "scene"

def get_or_create_principled_material(name, color, emission_strength=1):
    mat = bpy.data.materials.get(name)

    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True

    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")

    if bsdf:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color

        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = color

        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat

def world_normal(obj, local_normal):
    normal_matrix = obj.matrix_world.inverted().transposed().to_3x3()
    normal = normal_matrix @ local_normal

    if normal.length == 0:
        return Vector((0, 0, 1))

    return normal.normalized()

def edge_offset_direction(normals, v1, v2, center):
    direction = Vector((0, 0, 0))

    for normal in normals:
        direction += normal

    if direction.length > 0:
        return direction.normalized()

    midpoint = (v1 + v2) * 0.5
    direction = midpoint - center

    if direction.length > 0:
        return direction.normalized()

    return Vector((0, 0, 1))

def sanitize_object_name(name):
    name = re.sub(r"[^0-9A-Za-z_]+", "_", name).strip("_")
    return name or "Mesh"

def make_outline_object_name(meshes, source_label):
    if source_label == "scene":
        return LINE_OBJECT_NAME_PREFIX + "_Scene"

    names = sorted(obj.name for obj in meshes)

    if len(names) == 1:
        return LINE_OBJECT_NAME_PREFIX + "_" + sanitize_object_name(names[0])

    digest = hashlib.sha1("|".join(names).encode("utf-8")).hexdigest()[:8]
    return "{prefix}_Selected_{count}_{digest}".format(
        prefix=LINE_OBJECT_NAME_PREFIX,
        count=len(names),
        digest=digest
    )

target_meshes, source_label = get_scene_meshes()

if not target_meshes:
    raise Exception("No mesh objects found in selection or scene.")

line_object_name = make_outline_object_name(target_meshes, source_label)
model_color = hex_to_linear_rgba(MODEL_COLOR_HEX)

line_mat = get_or_create_principled_material(
    LINE_MATERIAL_NAME,
    (1, 1, 1, 1),
    1
)

model_mat = get_or_create_principled_material(
    MODEL_MATERIAL_NAME,
    model_color,
    1
)

for obj in target_meshes:
    obj.data.materials.clear()
    obj.data.materials.append(model_mat)

old_obj = bpy.data.objects.get(line_object_name)
if old_obj:
    bpy.data.objects.remove(old_obj, do_unlink=True)

curve = bpy.data.curves.new(line_object_name, type="CURVE")
curve.dimensions = "3D"
curve.resolution_u = 1
curve.bevel_depth = LINE_RADIUS
curve.bevel_resolution = 1

world_points = []

for obj in target_meshes:
    for vertex in obj.data.vertices:
        world_points.append(obj.matrix_world @ vertex.co)

if not world_points:
    raise Exception("Target mesh objects have no vertices.")

center = sum(world_points, Vector((0, 0, 0))) / len(world_points)
angle_threshold = math.radians(ANGLE_THRESHOLD_DEGREES)
outline_edge_count = 0

for obj in target_meshes:
    mesh = obj.data
    edge_faces = defaultdict(list)

    for poly in mesh.polygons:
        for edge_key in poly.edge_keys:
            key = tuple(sorted(edge_key))
            edge_faces[key].append(poly.index)

    for edge in mesh.edges:
        key = tuple(sorted(edge.vertices))
        faces = edge_faces[key]

        keep = False
        normals = []

        v1 = obj.matrix_world @ mesh.vertices[edge.vertices[0]].co
        v2 = obj.matrix_world @ mesh.vertices[edge.vertices[1]].co

        if (v2 - v1).length < MIN_EDGE_LENGTH:
            continue

        if len(faces) == 1:
            keep = True
            normals.append(world_normal(obj, mesh.polygons[faces[0]].normal))
        elif len(faces) == 2:
            n1 = world_normal(obj, mesh.polygons[faces[0]].normal)
            n2 = world_normal(obj, mesh.polygons[faces[1]].normal)
            normals.extend((n1, n2))
            keep = n1.angle(n2) >= angle_threshold

        if not keep:
            continue

        offset = edge_offset_direction(normals, v1, v2, center) * OUTLINE_NORMAL_OFFSET
        v1 += offset
        v2 += offset

        spline = curve.splines.new("POLY")
        spline.points.add(1)
        spline.points[0].co = (v1.x, v1.y, v1.z, 1)
        spline.points[1].co = (v2.x, v2.y, v2.z, 1)

        outline_edge_count += 1

line_obj = bpy.data.objects.new(line_object_name, curve)
bpy.context.collection.objects.link(line_obj)
line_obj.data.materials.append(line_mat)

print(
    "Outlined {count} mesh object(s) from {source}; {edges} edge line(s) -> '{name}'; material '{material}' #{hex_color}.".format(
        count=len(target_meshes),
        source=source_label,
        edges=outline_edge_count,
        name=line_object_name,
        material=MODEL_MATERIAL_NAME,
        hex_color=MODEL_COLOR_HEX
    )
)
