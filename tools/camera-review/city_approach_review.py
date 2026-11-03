"""Inspect selected world-to-place approaches in a complete Blender city assembly.

blender -b city-box/build/city/city_build.blend --python tools/camera-review/city_approach_review.py -- --out <empty-dir> [--rotate name=degrees ...] [--places name ...]
Only the in-memory scene is changed; no blend or Unity assets are saved.
"""
import argparse
import json
import math
from pathlib import Path
import subprocess
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out', type=Path, required=True)
parser.add_argument('--rotate', nargs='*', default=[])
parser.add_argument('--places', nargs='+', default=['街角咖啡馆', '货运公司', '雕像', '剧院', '老街酒馆', '码头'])
parser.add_argument('--render', action='store_true')
parser.add_argument('--world-position', nargs=3, type=float, help='Blender world-camera XYZ; retain the fixed world orientation')
parser.add_argument('--camera-distance', nargs='*', default=[], help='Orbit author radius and lens: name=radius=millimetres')
parser.add_argument('--move', nargs='*', default=[])
parser.add_argument('--camera-pitch', nargs='*', default=[], help='Orbit author pitch, preserving radius and yaw')
parser.add_argument('--camera-yaw', nargs='*', default=[], help='Rotate only an authored camera around its explicit or ground interest')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = Path(__file__).resolve().parents[2]
out = args.out.resolve()
out.mkdir(parents=True, exist_ok=True)
assert not any(out.iterdir()), 'Output directory must be empty'
scene = bpy.context.scene
for value in args.rotate:
    name, degrees = value.rsplit('=', 1)
    ob = scene.objects[name]
    assert ob.parent is None
    ob.rotation_euler.z += math.radians(float(degrees))
for value in args.move:
    name, x, y = value.rsplit('=', 2)
    scene.objects[name].location.x = float(x)
    scene.objects[name].location.y = float(y)
bpy.context.view_layer.update()
for value in args.camera_distance:
    name, radius, lens = value.rsplit('=', 2)
    cam = scene.objects['Camera_' + name]
    focus = scene.objects['OrbitPivot_' + name].matrix_world.translation
    delta = cam.matrix_world.translation - focus
    matrix = cam.matrix_world.copy()
    matrix.translation = focus + delta.normalized() * float(radius)
    cam.matrix_world = matrix
    cam.data.lens = float(lens)
bpy.context.view_layer.update()
for value in args.camera_pitch:
    name, degrees = value.rsplit('=', 1)
    cam = scene.objects['Camera_' + name]
    focus = scene.objects['OrbitPivot_' + name].matrix_world.translation
    delta = cam.matrix_world.translation - focus
    radius = delta.length
    horizontal = Vector((delta.x, delta.y, 0)).normalized() * radius * math.cos(math.radians(float(degrees)))
    position = focus + horizontal + Vector((0, 0, radius * math.sin(math.radians(float(degrees)))))
    matrix = (focus - position).to_track_quat('-Z', 'Y').to_matrix().to_4x4()
    matrix.translation = position
    cam.matrix_world = matrix
bpy.context.view_layer.update()
for value in args.camera_yaw:
    name, yaw = value.rsplit('=', 1)
    cam = scene.objects['Camera_' + name]
    focus = scene.objects.get('FocusTarget_' + name) or scene.objects.get('OrbitPivot_' + name)
    pos = cam.matrix_world.translation
    forward = cam.matrix_world.to_quaternion() @ Vector((0, 0, -1))
    if focus:
        center = focus.matrix_world.translation
    else:
        assert forward.z < -.001
        # Ground reference height is the camera prefab root's placement height.
        center = pos + forward * ((cam.parent.matrix_world.translation.z - pos.z) / forward.z)
    rotation = Matrix.Rotation(math.radians(float(yaw)), 4, 'Z')
    target = center + rotation.to_3x3() @ (pos - center)
    matrix = (center - target).to_track_quat('-Z', 'Y').to_matrix().to_4x4()
    matrix.translation = target
    cam.matrix_world = matrix
bpy.context.view_layer.update()

def owner(ob):
    while ob.parent is not None:
        ob = ob.parent
    return ob.name

def U(v): return Vector((-v.x, v.z, -v.y))
def B(v): return Vector((-v[0], -v[2], v[1]))

def shot(cam):
    q = cam.matrix_world.to_quaternion()
    f, up = U(q @ Vector((0, 0, -1))).normalized(), U(q @ Vector((0, 1, 0))).normalized()
    right = up.cross(f).normalized()
    rotation = Matrix((right, f.cross(right), f)).transposed().to_quaternion()
    film = math.degrees(2 * math.atan(cam.data.sensor_height / (2 * cam.data.lens)))
    fov = math.degrees(2 * math.atan(math.tan(math.radians(film / 2)) / (16 / 9))) if abs(film - 39.5978) <= .5 else film
    row = dict(Name=cam.name, Mode=cam['drag'], Position=list(U(cam.matrix_world.translation)), Rotation=[rotation.x, rotation.y, rotation.z, rotation.w], Fov=fov, Near=cam.data.clip_start)
    suffix = cam.name.removeprefix('Camera_')
    focus = scene.objects.get('FocusTarget_' + suffix) or scene.objects.get('OrbitPivot_' + suffix)
    if focus is not None:
        row['Pivot'] = list(U(focus.matrix_world.translation))
    assert row['Mode'] != 'orbit' or 'Pivot' in row
    return row

world = shot(scene.objects['Camera_世界'])
if args.world_position:
    world['Position'] = list(U(Vector(args.world_position)))
dll = root / 'tools/camera-review/bin/Debug/net9.0/CameraReview.dll'
assert dll.exists(), 'Build CameraReview.csproj first'
routes = []
for place in args.places:
    target = shot(scene.objects['Camera_' + place])
    inp = out / (place + '-input.json')
    result = out / (place + '-routes.json')
    inp.write_text(json.dumps(dict(Cameras=[world, target], Samples=120, ModelScale=1, DefaultDuration=2), ensure_ascii=False, indent=2))
    subprocess.run(['dotnet', str(dll), str(inp), str(result)], check=True)
    sampled = json.loads(result.read_text())['Routes']
    forward = next(r for r in sampled if r['From'] == world['Name'])
    routes.append(forward)
    # Return begins at the resolved arrival pose, not the original authored Orbit camera.
    arrival = dict(target)
    frame = forward['Frames'][-1]
    f, up = Vector(frame['Forward']), Vector(frame['Up'])
    right = up.cross(f).normalized()
    rotation = Matrix((right, f.cross(right), f)).transposed().to_quaternion()
    arrival['Position'] = frame['Position']
    arrival['Rotation'] = [rotation.x, rotation.y, rotation.z, rotation.w]
    return_input = out / (place + '-return-input.json')
    return_result = out / (place + '-return-routes.json')
    return_input.write_text(json.dumps(dict(Cameras=[arrival, world], Samples=120, ModelScale=1, DefaultDuration=2), ensure_ascii=False, indent=2))
    subprocess.run(['dotnet', str(dll), str(return_input), str(return_result)], check=True)
    routes.append(next(r for r in json.loads(return_result.read_text())['Routes'] if r['To'] == world['Name']))
(out / 'routes.json').write_text(json.dumps(dict(Routes=routes), ensure_ascii=False, indent=2))
# Aggregate evaluated visible exterior triangles by city root, preserving external blocker identity.
verts, polys, face_names, subject_bounds = {}, {}, {}, {}
deps = bpy.context.evaluated_depsgraph_get()
for inst in deps.object_instances:
    ob = inst.object
    if ob.type != 'MESH' or ob.hide_render or ob.get('preview_only') or ob.get('outline') == 'proxy' or ob.name.startswith('描线') or ob.get('layer') == 'interior':
        continue
    ancestor = ob
    interior = False
    while ancestor is not None:
        interior |= ancestor.name.startswith('内部_')
        ancestor = ancestor.parent
    if interior:
        continue
    name = owner(ob)
    mesh = ob.to_mesh()
    try:
        points = [inst.matrix_world @ v.co for v in mesh.vertices]
        offset = len(verts.setdefault(name, []))
        verts[name].extend(points)
        polys.setdefault(name, []).extend([[offset + i for i in p.vertices] for p in mesh.polygons])
        face_names.setdefault(name, []).extend([ob.name] * len(mesh.polygons))
        if name in args.places and len(points) >= 4:
            lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
            hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
            dims = sorted(hi - lo)
            if hi.z - lo.z > max(1.0, max(hi.x - lo.x, hi.y - lo.y) * .08):
                subject_bounds.setdefault(name, []).append((dims[-1] * dims[-2], ob.name, lo, hi))
    finally:
        ob.to_mesh_clear()
trees = {name: BVHTree.FromPolygons(v, polys[name]) for name, v in verts.items() if v and polys[name]}

def hits(a, b, exclude=None):
    d = b - a
    if d.length < .00001: return []
    result = []
    for name, tree in trees.items():
        if name == exclude: continue
        contact = tree.ray_cast(a, d.normalized(), d.length - .001)
        if contact[0] is not None:
            result.append(name + '/' + face_names[name][contact[2]] if name == '世界' else name)
    return result


back = -(scene.objects['Camera_世界'].matrix_world.to_quaternion() @ Vector((0, 0, -1)))
world_yaw = math.degrees(math.atan2(back.x, -back.y))
report = []
for route in routes:
    if route['From'] != 'Camera_世界': continue
    place = route['To'].removeprefix('Camera_')
    # Surface probes on the three dominant exterior masses, excluding own-root self-occlusion.
    masses = sorted(subject_bounds[place], key=lambda r: r[0], reverse=True)[:3]
    probes = []
    for _, _, lo, hi in masses:
        for x in (.15, .5, .85):
            for z in (.25, .6, .9):
                probes.append(Vector((lo.x + (hi.x - lo.x) * x, (lo.y + hi.y) / 2, lo.z + (hi.z - lo.z) * z)))
    occlusion, collision = [], []
    previous = None
    for frame in route['Frames']:
        p = B(frame['Position'])
        if previous is not None:
            names = hits(previous, p)
            if names: collision.append(dict(progress=frame['Progress'], roots=names))
        previous = p
        if frame['Progress'] < .3 or round(frame['Progress'] * 120) % 6: continue
        counts = {}
        for probe in probes:
            for name in hits(p, probe, exclude=place): counts[name] = counts.get(name, 0) + 1
        if counts: occlusion.append(dict(progress=frame['Progress'], blockers=counts, probes=len(probes)))
    cam = scene.objects['Camera_' + place]
    focus = scene.objects.get('OrbitPivot_' + place) or scene.objects.get('FocusTarget_' + place)
    delta = cam.matrix_world.translation - focus.matrix_world.translation if focus else -(cam.matrix_world.to_quaternion() @ Vector((0, 0, -1)))
    report.append(dict(place=place, path=route['Path'], map_south_deviation=math.degrees(math.atan2(delta.x, -delta.y)), world_view_deviation=(math.degrees(math.atan2(delta.x, -delta.y)) - world_yaw + 180) % 360 - 180, masses=[m[1] for m in masses], body_crossings=collision, external_occlusion=occlusion))
return_checks = []
for route in routes:
    if route['To'] != 'Camera_世界': continue
    crossings = []
    for a, b in zip(route['Frames'], route['Frames'][1:]):
        blockers = hits(B(a['Position']), B(b['Position']))
        if blockers: crossings.append(dict(progress=b['Progress'], roots=blockers))
    return_checks.append(dict(place=route['From'].removeprefix('Camera_'), path=route['Path'], body_crossings=crossings))
(out / 'geometry.json').write_text(json.dumps(dict(source=bpy.data.filepath, world_view_yaw=world_yaw, rotations=args.rotate, moves=args.move, world_position=args.world_position, camera_distances=args.camera_distance, camera_yaws=args.camera_yaw, camera_pitches=args.camera_pitch, returns=return_checks, scope='current assembled exterior geometry; representative world start; dominant mass probes; own-root occlusion excluded; no dynamic visibility or near-plane sweep proof', places=report), ensure_ascii=False, indent=2))
if args.render:
    sys.path.insert(0, str(root / 'city-box/pipeline'))
    import render as R
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 640, 360, 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.render.use_sequencer = scene.render.use_compositing = False
    shading = scene.display.shading
    shading.light, shading.color_type, shading.show_shadows, shading.show_cavity = 'STUDIO', 'OBJECT', True, True
    shading.background_type = 'WORLD'
    scene.world.color = (.055, .055, .065)
    scene.view_settings.view_transform = 'Standard'
    data = bpy.data.cameras.new('_approach_review')
    cam = bpy.data.objects.new('_approach_review', data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    data.sensor_fit, data.sensor_height, data.clip_end = 'VERTICAL', 24, 10000
    for route in routes:
        if route['From'] != 'Camera_世界': continue
        place = route['To'].removeprefix('Camera_')
        for ob in scene.objects:
            if ob.type == 'MESH': ob.color = (.88, .48, .15, 1) if owner(ob) == place else (.45, .48, .53, 1)
        for progress in (.5, .75, 1):
            frame = min(route['Frames'], key=lambda r: abs(r['Progress'] - progress))
            R.set_focus(scene, scene.objects[place]); scene.use_nodes = False
            for ob in scene.objects:
                if ob.type == 'MESH' and ob.get('layer') == 'interior':
                    ob.hide_render = owner(ob) != place
            data.lens = 12 / math.tan(math.radians(frame['Fov'] / 2))
            data.clip_start = frame['Near']
            f, up = B(frame['Forward']).normalized(), B(frame['Up']).normalized()
            right = f.cross(up).normalized()
            cam.location, cam.rotation_euler = B(frame['Position']), Matrix((right, right.cross(f), -f)).transposed().to_euler()
            scene.render.filepath = str(out / (place + '-' + str(round(progress * 100)) + '.png'))
            bpy.ops.render.render(write_still=True)
print('CITY_APPROACH_REVIEW', str(out), flush=True)
