"""Build the old-street bar "Blue Hour" with a hand-bent neon script sign.

Run headless:

    blender -b -P neon_bar_bluehour.py -- --out <path.blend> \
        --preview <lit.png> --preview-broken <broken.png>

The sign is real tube geometry. Every letter is a polyline of straight runs;
hard corners are replaced with short arcs before the profile is swept, which is
what a bender actually does -- glass turns through a radius, it does not fold.
The alphabet is an oblique art-deco monoline: tall, geometric, slanted.

Tubes are split into separate objects so the runtime can light them
independently. That is what lets one run stutter and spit sparks while the rest
of the sign stays on.

Objects the Unity side cares about:

    Neon_Lit_*            tubes that stay on
    Neon_Frame_*          the deco rails around the wordmark
    Neon_Flicker_*        the failing run: H's left stem + crossbar
    Neon_Dead_*           tubes that never light again (dark glass)
    SparkPoint_*          empties at the failing electrode / junction box

Everything else matches the project's existing look: one dark blue body
material plus white emissive edge lines (see model-outline.py).
"""

import math
import sys
from collections import defaultdict

import bmesh
import bpy
from mathutils import Vector

# --- project look, kept in sync with model-outline.py -----------------------

MODEL_MATERIAL_NAME = "M_Dark_Blue_Model"
MODEL_COLOR_HEX = "0A142A"
LINE_MATERIAL_NAME = "M_White_Emission_Lines"
LINE_OBJECT_NAME = "OutlineLines_BlueHourBar"
LINE_RADIUS = 0.005
ANGLE_THRESHOLD_DEGREES = 50
MIN_EDGE_LENGTH = 0.02
OUTLINE_NORMAL_OFFSET = 0.003

# --- sign ------------------------------------------------------------------

NEON_LIT_HEX = "4FC3FF"
NEON_FRAME_HEX = "FF9E4A"
NEON_TRIM_HEX = "FF9E4A"
NEON_DEAD_HEX = "141821"
GLASS_HEX = "070B14"

SIGN_WIDTH = 4.30         # metres, wordmark plus its deco rails
SIGN_CENTER_Z = 4.25
SIGN_PLANE_Y = -2.30      # tubes stand this far off the facade
SIGN_MARGIN = 0.22        # backer panel margin around the sign

TUBE_RADIUS = 0.042       # glyph units, scaled with the sign
FRAME_TUBE_SCALE = 0.7
TUBE_BEVEL_RESOLUTION = 2

CORNER_RADIUS = 0.11
CORNER_STEPS = 4
SLANT = 0.14
LETTER_GAP = 0.16
WORD_GAP = 0.45

# ---------------------------------------------------------------------------
# Glyph library -- an oblique art-deco monoline.
#
# Units: baseline y = 0, x-height y = 1.0, cap y = 1.7. Points are corners of a
# polyline; round_corners softens them into bends before sweeping. The slant is
# a shear applied after layout, so the alphabet stays easy to edit upright.
# ---------------------------------------------------------------------------

GLYPHS = {
    # Three tubes: stem, then a bowl teed off each side of the waist. The two
    # bowls end 0.06 apart so their tubes merge into one solid waist bar
    # instead of leaving the slot a single shared point would.
    "B": {
        "advance": 0.72,
        "strokes": [
            {"pts": [(0.05, 0.00), (0.05, 1.70)]},
            {"pts": [(0.05, 1.70), (0.44, 1.70), (0.62, 1.54),
                     (0.62, 1.20), (0.46, 1.02), (0.05, 1.02)]},
            {"pts": [(0.05, 0.96), (0.50, 0.96), (0.70, 0.78),
                     (0.70, 0.18), (0.52, 0.00), (0.05, 0.00)]},
        ],
    },
    "H": {
        "advance": 0.78,
        "strokes": [
            {"pts": [(0.06, 1.70), (0.06, 0.00)]},
            {"pts": [(0.66, 1.70), (0.66, 0.00)]},
            {"pts": [(-0.06, 1.02), (0.78, 1.02)]},
        ],
    },
    "l": {
        "advance": 0.40,
        "strokes": [
            {"pts": [(0.05, 1.70), (0.05, 0.14), (0.20, 0.00), (0.40, 0.00)]},
        ],
    },
    "u": {
        "advance": 0.48,
        "strokes": [
            {"pts": [(0.00, 1.00), (0.00, 0.16), (0.14, 0.00), (0.34, 0.00),
                     (0.48, 0.16), (0.48, 1.00)]},
        ],
    },
    # The aperture is closed down to a short notch: the bar runs the full width
    # and the lower terminal kicks back up to meet it.
    "e": {
        "advance": 0.54,
        "strokes": [
            {"pts": [(0.54, 0.86), (0.40, 1.00), (0.16, 1.00), (0.00, 0.84),
                     (0.00, 0.18), (0.16, 0.00), (0.38, 0.00), (0.54, 0.16),
                     (0.54, 0.32)]},
            {"pts": [(0.00, 0.56), (0.54, 0.56)]},
        ],
    },
    "o": {
        "advance": 0.50,
        "strokes": [
            {"pts": [(0.12, 1.00), (0.38, 1.00), (0.50, 0.84), (0.50, 0.16),
                     (0.38, 0.00), (0.12, 0.00), (0.00, 0.16), (0.00, 0.84)],
             "closed": True},
        ],
    },
    "r": {
        "advance": 0.46,
        "strokes": [
            {"pts": [(0.46, 0.86), (0.38, 1.00), (0.14, 1.00), (0.00, 0.84),
                     (0.00, 0.00)]},
        ],
    },
}


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def srgb_channel_to_linear(value):
    value = value / 255

    if value <= 0.04045:
        return value / 12.92

    return ((value + 0.055) / 1.055) ** 2.4


def hex_to_linear_rgba(hex_color):
    hex_color = hex_color.lstrip("#")
    r, g, b = (int(hex_color[i:i + 2], 16) for i in (0, 2, 4))
    return (
        srgb_channel_to_linear(r),
        srgb_channel_to_linear(g),
        srgb_channel_to_linear(b),
        1,
    )


def get_or_create_principled_material(name, color, emission_color=None, emission_strength=0.0):
    mat = bpy.data.materials.get(name)

    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True

    bsdf = mat.node_tree.nodes.get("Principled BSDF")

    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.55
        bsdf.inputs["Emission Color"].default_value = emission_color or color
        bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat


def link(obj, collection):
    collection.objects.link(obj)
    return obj


def add_mesh(name, verts, faces, collection):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    mesh.update()
    return link(bpy.data.objects.new(name, mesh), collection)


def add_box(name, lo, hi, collection):
    """Axis-aligned box from two opposite corners."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    verts = [
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1),
    ]
    faces = [
        (0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
        (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),
    ]
    return add_mesh(name, verts, faces, collection)


def add_slab(name, top_quad, thickness, collection):
    """Slanted slab: four top corners, extruded straight down."""
    top = [Vector(p) for p in top_quad]
    bottom = [p - Vector((0, 0, thickness)) for p in top]
    verts = [tuple(p) for p in top + bottom]
    faces = [
        (0, 1, 2, 3), (7, 6, 5, 4),
        (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0),
    ]
    return add_mesh(name, verts, faces, collection)


def add_cylinder(name, start, end, radius, collection, segments=6):
    """Six sides by default: the facets stay above the outline angle threshold,
    so round props still read as silhouettes in the project's line style."""
    axis = Vector(end) - Vector(start)
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segments,
        radius1=radius, radius2=radius, depth=axis.length,
    )
    bm.to_mesh(mesh)
    bm.free()

    obj = link(bpy.data.objects.new(name, mesh), collection)
    obj.location = (Vector(start) + Vector(end)) * 0.5
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = axis.to_track_quat("Z", "Y")
    return obj


def add_empty(name, location, collection, size=0.12):
    obj = link(bpy.data.objects.new(name, None), collection)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = size
    obj.location = location
    return obj


def assign(objects, material):
    for obj in objects:
        obj.data.materials.clear()
        obj.data.materials.append(material)


# ---------------------------------------------------------------------------
# neon tubes
# ---------------------------------------------------------------------------

def round_corners(points, radius=CORNER_RADIUS, steps=CORNER_STEPS, closed=False):
    """Replace hard corners with short arcs, the radius a bender can hold."""
    pts = [Vector(p) for p in points]

    if closed:
        pts = [pts[-1]] + pts + [pts[0]]

    out = [tuple(pts[0])]

    for index in range(1, len(pts) - 1):
        before, corner, after = pts[index - 1], pts[index], pts[index + 1]
        d1, d2 = before - corner, after - corner

        if d1.length < 1e-6 or d2.length < 1e-6:
            out.append(tuple(corner))
            continue

        if abs(d1.angle(d2) - math.pi) < math.radians(6):
            out.append(tuple(corner))
            continue

        r = min(radius, d1.length * 0.45, d2.length * 0.45)
        start = corner + d1.normalized() * r
        end = corner + d2.normalized() * r

        for step in range(steps + 1):
            t = step / steps
            out.append(tuple((1 - t) ** 2 * start + 2 * (1 - t) * t * corner + t ** 2 * end))

    out.append(tuple(pts[-1]))

    return out[1:-1] if closed else out


def make_tube(name, points_3d, closed, collection, radius):
    """Sweep a round profile along the bent centerline, capped like real glass."""
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = TUBE_BEVEL_RESOLUTION
    curve.resolution_u = 1
    curve.use_fill_caps = True

    spline = curve.splines.new("POLY")
    spline.points.add(len(points_3d) - 1)

    for point, co in zip(spline.points, points_3d):
        point.co = (*co, 1)

    spline.use_cyclic_u = closed

    return link(bpy.data.objects.new(name, curve), collection)


def word_strokes(word, origin):
    """Lay a word out as bent 2D strokes in glyph units."""
    strokes = []
    pen = 0.0

    for char in word:
        glyph = GLYPHS[char]

        for index, stroke in enumerate(glyph["strokes"]):
            closed = stroke.get("closed", False)
            strokes.append({
                "char": char,
                "word": None,
                "role": f"s{index}",
                "closed": closed,
                "pts": round_corners([(x + pen, y) for (x, y) in stroke["pts"]], closed=closed),
            })

        pen += glyph["advance"] + LETTER_GAP

    ox, oy = origin
    for stroke in strokes:
        stroke["pts"] = [(ox + x + y * SLANT, oy + y) for (x, y) in stroke["pts"]]

    return strokes, pen - LETTER_GAP


def stroke_bounds(strokes):
    xs = [x for stroke in strokes for (x, _) in stroke["pts"]]
    ys = [y for stroke in strokes for (_, y) in stroke["pts"]]
    return min(xs), min(ys), max(xs), max(ys)


# ---------------------------------------------------------------------------
# white edge lines, same treatment model-outline.py gives the other buildings
# ---------------------------------------------------------------------------

def world_normal(obj, local_normal):
    matrix = obj.matrix_world.inverted().transposed().to_3x3()
    normal = matrix @ local_normal
    return normal.normalized() if normal.length else Vector((0, 0, 1))


def build_outline(objects, collection):
    threshold = math.radians(ANGLE_THRESHOLD_DEGREES)
    segments = []

    for obj in objects:
        mesh = obj.data
        matrix = obj.matrix_world
        edge_faces = defaultdict(list)

        for poly in mesh.polygons:
            for key in poly.edge_keys:
                edge_faces[key].append(poly)

        for key, faces in edge_faces.items():
            v1 = matrix @ mesh.vertices[key[0]].co
            v2 = matrix @ mesh.vertices[key[1]].co

            if (v2 - v1).length < MIN_EDGE_LENGTH:
                continue

            normals = [world_normal(obj, face.normal) for face in faces]

            if len(normals) >= 2 and normals[0].angle(normals[1]) < threshold:
                continue

            direction = Vector((0, 0, 0))
            for normal in normals:
                direction += normal

            offset = (direction.normalized() * OUTLINE_NORMAL_OFFSET
                      if direction.length else Vector((0, 0, 0)))
            segments.append((v1 + offset, v2 + offset))

    curve = bpy.data.curves.new(LINE_OBJECT_NAME, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = LINE_RADIUS
    curve.bevel_resolution = 0
    curve.resolution_u = 1

    for v1, v2 in segments:
        spline = curve.splines.new("POLY")
        spline.points.add(1)
        spline.points[0].co = (*v1, 1)
        spline.points[1].co = (*v2, 1)

    return link(bpy.data.objects.new(LINE_OBJECT_NAME, curve), collection)


# ---------------------------------------------------------------------------
# the sign
# ---------------------------------------------------------------------------

# Tube runs wired as separate circuits. Killing one changes what the sign says,
# which is the point: see READINGS below.
CIRCUIT_BLUE = "Blue"        # the whole first word
CIRCUIT_H_STEM = "HStem"     # H's left stem plus its crossbar -- together a "t"
CIRCUIT_H_RIGHT = "HRight"   # H's right stem -- the failing run
CIRCUIT_OUR = "Our"          # o, u, r

# What is legible with each circuit dark. The flicker only drives H_RIGHT; the
# rest are here for scenes that want to cut power to a run on purpose.
READINGS = {
    (): "Blue Hour",
    (CIRCUIT_H_RIGHT,): "Blue tour",
    (CIRCUIT_H_RIGHT, CIRCUIT_BLUE): "tour",
    (CIRCUIT_H_STEM, CIRCUIT_H_RIGHT): "Blue our",
    (CIRCUIT_H_STEM, CIRCUIT_H_RIGHT, CIRCUIT_BLUE): "our",
    (CIRCUIT_H_STEM, CIRCUIT_H_RIGHT, CIRCUIT_OUR): "Blue",
    (CIRCUIT_BLUE,): "Hour",
}


def stroke_circuit(stroke):
    if stroke["char"] == "H":
        return CIRCUIT_H_RIGHT if stroke["role"] == "s1" else CIRCUIT_H_STEM

    return CIRCUIT_BLUE if stroke["word"] == "Blue" else CIRCUIT_OUR


def build_sign(collection):
    """Bend the tubes, then wire them into circuits that read differently."""
    blue, width_blue = word_strokes("Blue", origin=(0.0, 0.0))
    hour, _ = word_strokes("Hour", origin=(width_blue + WORD_GAP, 0.0))

    for stroke in blue:
        stroke["word"] = "Blue"
    for stroke in hour:
        stroke["word"] = "Hour"

    letters = blue + hour

    # deco rails: a rule above and below, turning down at each end
    x0, y0, x1, y1 = stroke_bounds(letters)
    left, right = x0 - 0.30, x1 + 0.30
    top, bottom = y1 + 0.26, y0 - 0.26
    frame = [
        {"role": "top",
         "pts": round_corners([(left, top - 0.24), (left, top), (right, top), (right, top - 0.24)])},
        {"role": "bottom",
         "pts": round_corners([(left, bottom + 0.24), (left, bottom), (right, bottom), (right, bottom + 0.24)])},
    ]

    # fit the whole assembly to the requested width, centred on the facade
    gx0, gy0, gx1, gy1 = stroke_bounds(letters + frame)
    scale = SIGN_WIDTH / (gx1 - gx0)
    mid_x, mid_y = (gx0 + gx1) * 0.5, (gy0 + gy1) * 0.5

    def place(points):
        return [((x - mid_x) * scale, SIGN_PLANE_Y, SIGN_CENTER_Z + (y - mid_y) * scale)
                for (x, y) in points]

    circuits = {CIRCUIT_BLUE: [], CIRCUIT_H_STEM: [], CIRCUIT_H_RIGHT: [], CIRCUIT_OUR: []}
    rails = []
    anchors = {}

    for index, stroke in enumerate(letters):
        points = place(stroke["pts"])
        circuit = stroke_circuit(stroke)
        tube = make_tube(f"Neon_{circuit}_{index:02d}_{stroke['char']}", points,
                         stroke["closed"], collection, TUBE_RADIUS * scale)
        circuits[circuit].append(tube)

        if circuit == CIRCUIT_H_RIGHT:
            anchors["stem_top"] = points[0]
            anchors["stem_bottom"] = points[-1]

    for stroke in frame:
        rails.append(make_tube(f"Neon_Frame_{stroke['role']}", place(stroke["pts"]),
                               False, collection, TUBE_RADIUS * FRAME_TUBE_SCALE * scale))

    sign_bounds = (
        (gx0 - mid_x) * scale, SIGN_CENTER_Z + (gy0 - mid_y) * scale,
        (gx1 - mid_x) * scale, SIGN_CENTER_Z + (gy1 - mid_y) * scale,
    )

    return circuits, rails, anchors, sign_bounds


def build_awning_trim(awning_front_y, awning_front_z, collection):
    """Amber tube along the awning edge, with a burnt-out stretch in the middle."""
    trim_y, trim_z = awning_front_y - 0.06, awning_front_z - 0.38
    runs = [
        ("Neon_Lit_Trim_L", -2.34, -0.86, False),
        ("Neon_Dead_Trim_M", -0.86, 0.74, True),
        ("Neon_Lit_Trim_R", 0.74, 2.34, False),
    ]

    lit = []
    dead = []

    for name, start_x, end_x, is_dead in runs:
        tube = make_tube(name, [(start_x, trim_y, trim_z), (end_x, trim_y, trim_z)],
                         False, collection, 0.022)
        (dead if is_dead else lit).append(tube)

    return lit, dead


# ---------------------------------------------------------------------------
# the bar itself
# ---------------------------------------------------------------------------

# Facade faces -Y (the street). X is the street direction, Z is up.
FACADE_Y = -2.00
BACK_Y = 2.00
HALF_W = 3.60
RECESS_Y = -1.40
GROUND_TOP = 3.05
ROOF_TOP = 7.00
AWNING_FRONT_Y = -3.30
AWNING_FRONT_Z = 2.36


def build_building(sign_bounds, collection):
    body = []
    glass = []

    sign_x0, sign_z0, sign_x1, sign_z1 = sign_bounds
    backer_x = max(abs(sign_x0), abs(sign_x1)) + SIGN_MARGIN
    backer_z0, backer_z1 = sign_z0 - SIGN_MARGIN, sign_z1 + SIGN_MARGIN

    # ground floor: two piers frame a recessed entrance
    body.append(add_box("Bar_Pier_L", (-HALF_W, FACADE_Y, 0.0), (-2.30, BACK_Y, GROUND_TOP), collection))
    body.append(add_box("Bar_Pier_R", (2.30, FACADE_Y, 0.0), (HALF_W, BACK_Y, GROUND_TOP), collection))
    body.append(add_box("Bar_RecessBack", (-2.30, RECESS_Y, 0.0), (2.30, BACK_Y, GROUND_TOP), collection))
    body.append(add_box("Bar_Lintel", (-2.30, FACADE_Y, 2.60), (2.30, RECESS_Y, GROUND_TOP), collection))
    body.append(add_box("Bar_RecessFloor", (-2.30, FACADE_Y, 0.0), (2.30, RECESS_Y, 0.10), collection))

    # upper mass, belt course, cornice
    body.append(add_box("Bar_Upper", (-HALF_W, FACADE_Y, GROUND_TOP), (HALF_W, BACK_Y, ROOF_TOP), collection))
    body.append(add_box("Bar_BeltCourse", (-HALF_W - 0.12, -2.12, 2.95), (HALF_W + 0.12, BACK_Y, 3.15), collection))
    body.append(add_box("Bar_Cornice", (-HALF_W - 0.16, -2.18, ROOF_TOP), (HALF_W + 0.16, BACK_Y, ROOF_TOP + 0.34), collection))
    body.append(add_box("Bar_Parapet", (-HALF_W - 0.08, -2.10, ROOF_TOP + 0.34), (HALF_W + 0.08, 1.90, ROOF_TOP + 0.70), collection))

    # entrance
    body.append(add_box("Bar_DoorFrame", (-0.74, -1.48, 0.10), (0.74, -1.36, 2.40), collection))
    body.append(add_box("Bar_DoorLeaf_L", (-0.66, -1.45, 0.14), (-0.02, -1.39, 2.32), collection))
    body.append(add_box("Bar_DoorLeaf_R", (0.02, -1.45, 0.39), (0.66, -1.39, 2.32), collection))
    body.append(add_box("Bar_DoorStep", (-0.90, -1.70, 0.10), (0.90, -1.36, 0.20), collection))
    body.append(add_cylinder("Bar_DoorHandle_L", (-0.14, -1.50, 1.10), (-0.14, -1.50, 1.42), 0.025, collection))
    body.append(add_cylinder("Bar_DoorHandle_R", (0.14, -1.50, 1.10), (0.14, -1.50, 1.42), 0.025, collection))

    # storefront windows either side of the door
    for side, sign in (("L", -1), ("R", 1)):
        x0, x1 = sorted((sign * 0.95, sign * 2.14))
        body.append(add_box(f"Bar_WinFrame_{side}", (x0 - 0.06, -1.47, 0.78), (x1 + 0.06, -1.37, 2.32), collection))
        glass.append(add_box(f"Bar_WinGlass_{side}", (x0, -1.44, 0.84), (x1, -1.41, 2.26), collection))
        body.append(add_box(f"Bar_WinMullion_{side}", ((x0 + x1) * 0.5 - 0.03, -1.46, 0.84),
                            ((x0 + x1) * 0.5 + 0.03, -1.40, 2.26), collection))
        body.append(add_box(f"Bar_WinSill_{side}", (x0 - 0.10, -1.52, 0.70), (x1 + 0.10, -1.36, 0.78), collection))

    # awning over the recess, sloping down toward the street
    body.append(add_slab(
        "Bar_Awning",
        [(-2.44, FACADE_Y, 2.80), (2.44, FACADE_Y, 2.80),
         (2.44, AWNING_FRONT_Y, AWNING_FRONT_Z), (-2.44, AWNING_FRONT_Y, AWNING_FRONT_Z)],
        0.10, collection))
    body.append(add_box("Bar_AwningValance", (-2.44, AWNING_FRONT_Y, AWNING_FRONT_Z - 0.44),
                        (2.44, AWNING_FRONT_Y + 0.06, AWNING_FRONT_Z - 0.10), collection))

    for side, x in (("L", -2.36), ("R", 2.36)):
        body.append(add_cylinder(f"Bar_AwningBrace_{side}", (x, FACADE_Y, 2.06),
                                 (x, AWNING_FRONT_Y + 0.10, AWNING_FRONT_Z - 0.06), 0.035, collection))

    # sign backer panel and its hood, sized around the tubes
    body.append(add_box("Bar_SignBacker", (-backer_x, -2.16, backer_z0), (backer_x, FACADE_Y, backer_z1), collection))
    body.append(add_slab(
        "Bar_SignHood",
        [(-backer_x - 0.08, -2.18, backer_z1 + 0.14), (backer_x + 0.08, -2.18, backer_z1 + 0.14),
         (backer_x + 0.08, -2.64, backer_z1), (-backer_x - 0.08, -2.64, backer_z1)],
        0.07, collection))

    standoff_x = backer_x - 0.30
    for index, x in enumerate((-standoff_x, -standoff_x * 0.34, standoff_x * 0.34, standoff_x)):
        for row, z in enumerate((backer_z0 + 0.28, backer_z1 - 0.28)):
            body.append(add_cylinder(f"Bar_SignStandoff_{index}{row}",
                                     (x, -2.16, z), (x, SIGN_PLANE_Y, z), 0.03, collection))

    # upper floor windows, clear of the sign hood
    window_z0 = backer_z1 + 0.42
    window_z1 = window_z0 + 0.98
    for index, cx in enumerate((-2.36, 0.0, 2.36)):
        body.append(add_box(f"Bar_UpperFrame_{index}", (cx - 0.62, -2.06, window_z0), (cx + 0.62, -1.90, window_z1), collection))
        glass.append(add_box(f"Bar_UpperGlass_{index}", (cx - 0.54, -2.02, window_z0 + 0.07), (cx + 0.54, -1.98, window_z1 - 0.07), collection))
        body.append(add_box(f"Bar_UpperMullion_{index}", (cx - 0.03, -2.05, window_z0 + 0.07), (cx + 0.03, -1.96, window_z1 - 0.07), collection))
        body.append(add_box(f"Bar_UpperSill_{index}", (cx - 0.72, -2.14, window_z0 - 0.09), (cx + 0.72, -1.94, window_z0), collection))

    # a rattling air conditioner in the right-hand window
    body.append(add_box("Bar_AirCon", (1.92, -2.42, window_z0 + 0.07), (2.66, -2.00, window_z0 + 0.53), collection))
    body.append(add_box("Bar_AirConBracket", (1.98, -2.30, window_z0 - 0.03), (2.60, -2.06, window_z0 + 0.07), collection))

    # drain pipe down the left edge
    body.append(add_cylinder("Bar_DrainPipe", (-HALF_W + 0.10, -2.16, 0.0), (-HALF_W + 0.10, -2.16, ROOF_TOP + 0.30), 0.055, collection))
    for z in (1.6, 3.4, 5.2, 6.4):
        body.append(add_box("Bar_PipeClamp", (-HALF_W + 0.02, -2.24, z), (-HALF_W + 0.18, -2.08, z + 0.09), collection))

    # street
    body.append(add_box("Street_Sidewalk", (-6.40, -4.90, -0.14), (6.40, FACADE_Y, 0.0), collection))
    body.append(add_box("Street_Curb", (-6.40, -5.06, -0.14), (6.40, -4.90, 0.05), collection))
    body.append(add_cylinder("Prop_TrashCan", (-2.95, -2.66, 0.0), (-2.95, -2.66, 0.86), 0.30, collection))
    body.append(add_box("Prop_TrashLid", (-3.29, -3.00, 0.86), (-2.61, -2.32, 0.94), collection))
    body.append(add_box("Prop_Crate_A", (2.78, -2.90, 0.0), (3.42, -2.26, 0.58), collection))
    body.append(add_box("Prop_Crate_B", (2.90, -2.74, 0.58), (3.38, -2.34, 1.02), collection))

    return body, glass, backer_z0


def build_break_hardware(anchors, backer_z0, collection):
    """The junction box and conduit feeding the failing run, plus spark points.

    The failing tube is the H's right stem, so its two electrodes sit at the top
    and bottom of that stroke. The box hangs below the sign and the lead comes up
    from underneath, where it never crosses the lettering.
    """
    stem_top = Vector(anchors["stem_top"])
    stem_bottom = Vector(anchors["stem_bottom"])

    box_x = stem_bottom.x - 0.10
    box_z = backer_z0 - 0.22
    parts = [
        add_box("Bar_NeonJunctionBox", (box_x - 0.15, -2.26, box_z - 0.16),
                (box_x + 0.15, -2.02, box_z + 0.16), collection),
        add_cylinder("Bar_NeonConduit", (box_x, -2.13, box_z - 0.16), (box_x, -2.13, 2.62), 0.028, collection),
        add_cylinder("Bar_NeonLead", (box_x, -2.14, box_z + 0.16),
                     (stem_bottom.x, stem_bottom.y, stem_bottom.z), 0.018, collection),
    ]

    add_empty("SparkPoint_01_Junction", (box_x, -2.30, box_z + 0.18), collection)
    add_empty("SparkPoint_02_StemFoot", (stem_bottom.x, stem_bottom.y - 0.04, stem_bottom.z), collection)
    add_empty("SparkPoint_03_StemHead", (stem_top.x, stem_top.y - 0.04, stem_top.z), collection)

    return parts


# ---------------------------------------------------------------------------
# preview render
# ---------------------------------------------------------------------------

def render_preview(scene, path, collection):
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.lens = 40
    camera = link(bpy.data.objects.new("PreviewCamera", camera_data), collection)
    camera.location = (9.4, -16.4, 6.9)
    camera.rotation_euler = (math.radians(83), 0, math.radians(31))
    scene.camera = camera

    key_data = bpy.data.lights.new("PreviewKey", "AREA")
    key_data.energy = 220
    key_data.size = 7
    key = link(bpy.data.objects.new("PreviewKey", key_data), collection)
    key.location = (-7.0, -10.0, 9.0)
    key.rotation_euler = (math.radians(58), 0, math.radians(-38))

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.filepath = path
    scene.world = bpy.data.worlds.new("PreviewWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.006, 0.010, 0.020, 1)

    bpy.ops.render.render(write_still=True)

    for obj in (camera, key):
        bpy.data.objects.remove(obj, do_unlink=True)

    scene.camera = None


# ---------------------------------------------------------------------------

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    paths = {"--out": None, "--preview": None, "--preview-broken": None}

    for flag, value in zip(argv, argv[1:]):
        if flag in paths:
            paths[flag] = value

    return paths["--out"], paths["--preview"], paths["--preview-broken"]


def main():
    out_path, preview_path, broken_preview_path = parse_args()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    collection = scene.collection

    body_mat = get_or_create_principled_material(MODEL_MATERIAL_NAME, hex_to_linear_rgba(MODEL_COLOR_HEX))
    line_mat = get_or_create_principled_material(LINE_MATERIAL_NAME, (1, 1, 1, 1), emission_strength=1.0)
    glass_mat = get_or_create_principled_material("M_Window_Glass", hex_to_linear_rgba(GLASS_HEX))
    neon_lit_mat = get_or_create_principled_material(
        "M_Neon_Lit", hex_to_linear_rgba("0A1420"),
        emission_color=hex_to_linear_rgba(NEON_LIT_HEX), emission_strength=4.0)
    neon_flicker_mat = get_or_create_principled_material(
        "M_Neon_Flicker", hex_to_linear_rgba("0A1420"),
        emission_color=hex_to_linear_rgba(NEON_LIT_HEX), emission_strength=4.0)
    neon_frame_mat = get_or_create_principled_material(
        "M_Neon_Frame", hex_to_linear_rgba("140A04"),
        emission_color=hex_to_linear_rgba(NEON_FRAME_HEX), emission_strength=3.0)
    neon_trim_mat = get_or_create_principled_material(
        "M_Neon_Trim", hex_to_linear_rgba("140A04"),
        emission_color=hex_to_linear_rgba(NEON_TRIM_HEX), emission_strength=3.0)
    neon_dead_mat = get_or_create_principled_material("M_Neon_Dead", hex_to_linear_rgba(NEON_DEAD_HEX))

    circuits, rails, anchors, sign_bounds = build_sign(collection)
    flicker = circuits[CIRCUIT_H_RIGHT]
    lit = circuits[CIRCUIT_BLUE] + circuits[CIRCUIT_H_STEM] + circuits[CIRCUIT_OUR]
    body, glass, backer_z0 = build_building(sign_bounds, collection)
    trim_lit, trim_dead = build_awning_trim(AWNING_FRONT_Y, AWNING_FRONT_Z, collection)
    body += build_break_hardware(anchors, backer_z0, collection)

    assign(body, body_mat)
    assign(glass, glass_mat)
    assign(lit, neon_lit_mat)
    assign(flicker, neon_flicker_mat)
    assign(rails, neon_frame_mat)
    assign(trim_lit, neon_trim_mat)
    assign(trim_dead, neon_dead_mat)

    # cylinders carry their placement on the object transform, so matrix_world
    # has to be current before edges are read in world space
    bpy.context.view_layer.update()

    outline = build_outline([obj for obj in body if obj.type == "MESH"], collection)
    assign([outline], line_mat)

    anchor = add_empty("Anchor_BlueHourBar", (0.0, -2.00, 0.80), collection, size=0.3)
    pivot = add_empty("orbit pivot", (0.0, 0.0, 3.20), collection, size=0.3)

    # group the hierarchy so the Unity side can grab a whole run in one drag
    root = add_empty("BlueHourBar", (0, 0, 0), collection, size=0.5)
    spark_points = [obj for obj in collection.objects if obj.name.startswith("SparkPoint_")]

    groups = (
        ("Grp_Building", body + glass + [outline]),
        ("Grp_NeonBlue", circuits[CIRCUIT_BLUE]),
        ("Grp_NeonHStem", circuits[CIRCUIT_H_STEM]),
        ("Grp_NeonFlicker", circuits[CIRCUIT_H_RIGHT]),
        ("Grp_NeonOur", circuits[CIRCUIT_OUR]),
        ("Grp_NeonFrame", rails),
        ("Grp_NeonTrim", trim_lit),
        ("Grp_NeonDead", trim_dead),
        ("Grp_SparkPoints", spark_points),
    )

    for group_name, members in groups:
        group = add_empty(group_name, (0, 0, 0), collection, size=0.25)
        group.parent = root
        for obj in members:
            obj.parent = group

    for obj in (anchor, pivot):
        obj.parent = root

    if preview_path:
        render_preview(scene, preview_path, collection)

    if broken_preview_path:
        # what the sign looks like at the bottom of a stutter: the H's right stem
        # is dark glass, so the letter collapses to a "t" and it reads Blue tour
        assign(flicker, neon_dead_mat)
        render_preview(scene, broken_preview_path, collection)
        assign(flicker, neon_flicker_mat)

    if out_path:
        bpy.ops.wm.save_as_mainfile(filepath=out_path)

    print("[blue-hour] " + " ".join(f"{name}={len(tubes)}" for name, tubes in circuits.items())
          + f" frame={len(rails)} trim={len(trim_lit) + len(trim_dead)} body={len(body)}")


main()
