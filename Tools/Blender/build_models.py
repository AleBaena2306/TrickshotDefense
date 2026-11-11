# Trickshot Defense · modelos low poly generados por script (Blender 5.x como módulo: pip install bpy).
# Uso:  python build_models.py <carpeta_fbx> [<carpeta_previews> [<carpeta_retratos_ui>]]
# Genera un FBX por modelo, hecho de PIEZAS RÍGIDAS con el origen en su articulación (Head, Torso, ArmL, ArmR,
# LegL, LegR, Item...). El juego las anima por código (TrickshotDefense.Rig), así que no hay esqueleto ni clips.
# Convenciones: 1 unidad = 1 m; los personajes miran hacia -Y en Blender (= +Z en Unity); Z arriba.
# Materiales con nombre: «Skin» (igual en todos los deportistas), «Kit» (la prenda: el juego la tiñe con la
# equipación elegida), y el resto con su color final.
import bpy, sys, os, math
from mathutils import Vector

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
PREV = sys.argv[2] if len(sys.argv) > 2 else None
PORT = sys.argv[3] if len(sys.argv) > 3 else None   # retratos con fondo transparente para la UI
if PORT: os.makedirs(PORT, exist_ok=True)
os.makedirs(OUT, exist_ok=True)
if PREV: os.makedirs(PREV, exist_ok=True)

PALETTE = {
    "Skin": "#F2E3CC", "Kit": "#E8483F", "Eye": "#2B2F3A", "White": "#F7F5EF", "Black": "#23262E",
    "BallTennis": "#D8E84A", "BallBasket": "#F08A24", "BallBowling": "#5B3FD6", "Wood": "#C99A5B",
    "Rubber": "#D93A3A", "Strings": "#EDEBE0",
    "Steel": "#93A1B5", "SteelDark": "#3C4656", "RobotEye": "#FF4D4D", "Cone": "#FF8A2A", "Shield": "#27408B",
    "Warn": "#F5C542", "BossBody": "#7A3140", "Tyre": "#22252B",
    "Grass": "#6CC24A", "Grass2": "#5FB341", "Track": "#D9764A", "Line": "#FFFFFF", "Wall": "#F5F2EA",
    "Roof": "#2F6FDE", "Gold": "#F5C542", "Seat1": "#2F6FDE", "Seat2": "#E8483F", "Concrete": "#B9BEC8",
    "Podium": "#F7F5EF", "PodiumRing": "#2F6FDE", "Dirt": "#C9B07A",
}

def hex_rgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]  # sRGB -> lineal

_mats = {}
def mat(name):
    if name in _mats and _mats[name].name in bpy.data.materials: return _mats[name]
    m = bpy.data.materials.new(name)
    rgb = hex_rgb(PALETTE[name])
    m.diffuse_color = (*rgb, 1)
    try:
        m.use_nodes = True
    except Exception:
        pass
    if m.node_tree:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = (*rgb, 1)
            b.inputs["Roughness"].default_value = 1.0
            if "Metallic" in b.inputs: b.inputs["Metallic"].default_value = 0.0
    _mats[name] = m
    return m

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()

def _finish(o, m):
    o.data.materials.append(mat(m))
    return o

def box(center, size, m):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    o = bpy.context.object
    o.scale = size
    return _finish(o, m)

def sph(center, r, m, sub=2, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=center)
    o = bpy.context.object
    o.scale = scale
    return _finish(o, m)

def cyl(p1, p2, r1, r2, m, verts=8, scale_xy=(1, 1)):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=d.length, location=(p1 + p2) / 2)
    o = bpy.context.object
    o.scale = (scale_xy[0], scale_xy[1], 1)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return _finish(o, m)

def torus(center, R, r, m, normal=(0, 0, 1), seg=12):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=6, major_radius=R, minor_radius=r, location=center)
    o = bpy.context.object
    o.rotation_euler = Vector(normal).to_track_quat('Z', 'Y').to_euler()
    return _finish(o, m)

def part(name, objs, origin):
    """Une las mallas en una pieza con nombre y origen en la articulación."""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    o.data.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.context.scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    for p in o.data.polygons: p.use_smooth = False
    return o

def root(name):
    bpy.ops.object.empty_add(type='PLAIN_AXES', location=(0, 0, 0))
    e = bpy.context.object
    e.name = name
    return e

def child(c, p):
    bpy.ops.object.select_all(action='DESELECT')
    c.select_set(True); p.select_set(True)
    bpy.context.view_layer.objects.active = p
    bpy.ops.object.parent_set(type='OBJECT', keep_transform=True)

def export(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
                             object_types={'EMPTY', 'MESH'}, axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    if PREV: preview(name, PREV, 384, Vector((0.75, -1.0, 0.55)), False)
    if PORT and (name.startswith("athlete_") or name.startswith("robot_")):
        preview(name, PORT, 256, Vector((0.55, -1.0, 0.28)), True)

def preview(name, folder, res, view, transparent):
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    mid = (lo + hi) / 2; size = max((hi - lo).length, 0.5)
    bpy.ops.object.camera_add()
    cam = bpy.context.object
    direction = view.normalized()
    cam.location = mid + direction * size * (1.75 if transparent else 1.35)
    cam.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = 60
    s = bpy.context.scene
    s.camera = cam
    s.render.engine = 'BLENDER_WORKBENCH'
    s.display.shading.light = 'STUDIO'
    s.display.shading.color_type = 'MATERIAL'
    s.display.shading.show_shadows = True
    s.view_settings.view_transform = 'Standard'
    s.render.resolution_x = res; s.render.resolution_y = res
    s.render.film_transparent = transparent
    s.render.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
    s.render.filepath = os.path.join(folder, name + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)

# ------------------------------------------------------------------ Deportistas
# Mirando a -Y: la derecha del personaje es -X.
R, L = -1, 1
SHOULDER_Z, HIP_Z, HAND_Z = 1.26, 0.74, 0.80

def athlete(sport):
    reset()
    rt = root("Athlete")
    foot_mat = "Kit" if sport in ("soccer", "bowling") else "Skin"

    torso_objs = [cyl((0, 0, 0.74), (0, 0, 1.32), 0.20, 0.27, "Skin", 8, (1.15, 0.78)),
                  sph((0, 0, 0.74), 0.205, "Skin", 2, (1.12, 0.8, 0.8)),
                  sph((0, 0, 1.32), 0.27, "Skin", 2, (1.15, 0.78, 0.45))]
    if sport == "tennis":   # pantalón corto
        torso_objs.append(cyl((0, 0, 0.60), (0, 0, 0.88), 0.27, 0.235, "Kit", 8, (1.12, 0.84)))
    torso = part("Torso", torso_objs, (0, 0, HIP_Z))

    head_objs = [sph((0, 0, 1.70), 0.31, "Skin", 2),
                 sph((-0.11, -0.275, 1.74), 0.04, "Eye", 1), sph((0.11, -0.275, 1.74), 0.04, "Eye", 1)]
    if sport == "basket":   # cinta del pelo
        head_objs.append(cyl((0, 0, 1.80), (0, 0, 1.90), 0.305, 0.285, "Kit", 12))
    head = part("Head", head_objs, (0, 0, 1.40))

    def arm(side, name):
        sx = 0.345 * side
        hx = 0.44 * side
        objs = [sph((sx, 0, SHOULDER_Z), 0.095, "Skin", 1),
                cyl((sx, 0, SHOULDER_Z), (hx, 0, HAND_Z), 0.078, 0.066, "Skin"),
                sph((hx, 0, HAND_Z - 0.03), 0.098, "Skin", 1)]
        return part(name, objs, (sx, 0, SHOULDER_Z))

    def leg(side, name):
        x = 0.135 * side
        objs = [cyl((x, 0, HIP_Z), (x, 0, 0.11), 0.10, 0.082, "Skin")]
        if sport == "tennis":
            objs.append(cyl((x, 0, HIP_Z + 0.02), (x, 0, 0.50), 0.125, 0.13, "Kit"))
        big = 1.18 if foot_mat == "Kit" else 1.0
        objs.append(box((x, -0.07, 0.065), (0.17 * big, 0.31 * big, 0.13 * big), foot_mat))
        if sport == "bowling":   # zapato bicolor
            objs.append(box((x, -0.19, 0.07), (0.205, 0.10, 0.155), "White"))
        return part(name, objs, (x, 0, HIP_Z))

    arm_l, arm_r = arm(L, "ArmL"), arm(R, "ArmR")
    leg_l, leg_r = leg(L, "LegL"), leg(R, "LegR")
    for p in (torso, leg_l, leg_r): child(p, rt)
    for p in (head, arm_l, arm_r): child(p, torso)

    hand = Vector((0.44 * R, 0, HAND_Z - 0.03))
    item = None
    if sport == "tennis":
        a = hand + Vector((0, -0.02, 0)); b = hand + Vector((0, -0.24, 0.20))
        c = b + (b - a).normalized() * 0.20
        item = part("Item", [cyl(a, b, 0.028, 0.024, "Black"),
                             torus(c, 0.19, 0.024, "Kit", (1, 0, 0), 12),
                             cyl(c - Vector((0.008, 0, 0)), c + Vector((0.008, 0, 0)), 0.18, 0.18, "Strings", 12)], hand)
    elif sport == "pingpong":
        a = hand + Vector((0, -0.02, 0)); b = hand + Vector((0, -0.13, 0.10))
        c = b + (b - a).normalized() * 0.13
        item = part("Item", [cyl(a, b, 0.03, 0.03, "Wood"),
                             cyl(c - Vector((0.02, 0, 0)), c + Vector((0.02, 0, 0)), 0.15, 0.15, "Rubber", 12)], hand)
    elif sport == "basket":
        item = part("Item", [sph(hand + Vector((0.02, -0.17, 0.02)), 0.17, "BallBasket", 2)], hand)
    elif sport == "bowling":
        item = part("Item", [sph(hand + Vector((0, -0.03, -0.17)), 0.165, "BallBowling", 2)], hand)
    if item: child(item, arm_r)
    if sport == "soccer":   # el balón, a los pies
        ball = part("Ball", [soccer_ball((0.135 * R, -0.36, 0.13), 0.13)], (0.135 * R, -0.36, 0.13))
        child(ball, rt)
    export("athlete_" + sport)

def soccer_ball(center, r):
    o = sph(center, r, "White", 2)
    o.data.materials.append(mat("Black"))
    for i, p in enumerate(o.data.polygons):
        if i % 7 == 0 or i % 11 == 0: p.material_index = 1
    return o

# ------------------------------------------------------------------ Balones (proyectiles, radio real)
def balls():
    for name, m, r in (("ball_tennis", "BallTennis", 0.10), ("ball_basket", "BallBasket", 0.17),
                       ("ball_bowling", "BallBowling", 0.20), ("ball_pingpong", "White", 0.07)):
        reset(); part("Ball", [sph((0, 0, 0), r, m, 2)], (0, 0, 0)); export(name)
    reset(); part("Ball", [soccer_ball((0, 0, 0), 0.15)], (0, 0, 0)); export("ball_soccer")

# ------------------------------------------------------------------ Robots de entrenamiento (miran a -Y)
def eye_visor(y, z, w=0.26, h=0.07):
    return box((0, y, z), (w, 0.04, h), "RobotEye")

def robot_basic():
    reset(); rt = root("Robot")
    body = part("Body", [cyl((0, 0, 0.22), (0, 0, 1.05), 0.30, 0.24, "Steel", 10),
                         cyl((0, 0, 0.55), (0, 0, 0.66), 0.31, 0.30, "Warn", 10),
                         cyl((0, 0, 0.0), (0, 0, 0.22), 0.36, 0.33, "SteelDark", 10)], (0, 0, 0))
    head = part("Head", [sph((0, 0, 1.25), 0.27, "Steel", 2, (1, 1, 0.85)), eye_visor(-0.245, 1.27),
                         cyl((0, 0, 1.45), (0, 0, 1.62), 0.015, 0.015, "SteelDark", 6), sph((0, 0, 1.64), 0.04, "RobotEye", 1)],
                (0, 0, 1.05))
    def arm(side, name):
        x = 0.33 * side
        return part(name, [cyl((x, 0, 0.92), (x * 1.25, -0.05, 0.50), 0.06, 0.05, "SteelDark"),
                           sph((x * 1.25, -0.05, 0.46), 0.085, "Steel", 1)], (x, 0, 0.92))
    al, ar = arm(L, "ArmL"), arm(R, "ArmR")
    child(body, rt); child(head, body); child(al, body); child(ar, body)
    export("robot_basic")

def robot_runner():
    reset(); rt = root("Robot")
    body = part("Body", [cyl((0, 0, 0.16), (0, 0, 0.95), 0.27, 0.07, "Cone", 10),
                         cyl((0, 0, 0.42), (0, 0, 0.58), 0.205, 0.165, "White", 10),
                         box((0, 0, 0.14), (0.62, 0.62, 0.07), "Cone"),
                         eye_visor(-0.135, 0.72, 0.16, 0.055),
                         sph((0, 0, 0.97), 0.075, "SteelDark", 1)], (0, 0, 0.1))
    wheel = part("Wheel", [cyl((-0.07, 0, 0.11), (0.07, 0, 0.11), 0.11, 0.11, "Tyre", 10)], (0, 0, 0.11))
    child(body, rt); child(wheel, rt)
    export("robot_runner")

def robot_armored():
    reset(); rt = root("Robot")
    body = part("Body", [box((0, 0.05, 0.62), (0.62, 0.48, 0.82), "Steel"),
                         box((0, 0.05, 0.14), (0.74, 0.62, 0.22), "SteelDark"),
                         sph((0, 0.02, 1.16), 0.22, "SteelDark", 2, (1.1, 1, 0.8)), eye_visor(-0.19, 1.17, 0.24, 0.06)],
                (0, 0, 0))
    shield = part("Shield", [box((0, -0.36, 0.68), (0.98, 0.10, 1.10), "Shield"),
                             box((0, -0.42, 0.86), (0.70, 0.03, 0.10), "Warn"),
                             box((0, -0.42, 0.56), (0.70, 0.03, 0.10), "Warn"),
                             box((0, -0.28, 0.68), (0.12, 0.16, 0.12), "SteelDark")], (0, -0.30, 0.68))
    child(body, rt); child(shield, body)
    export("robot_armored")

def robot_flyer():
    reset(); rt = root("Robot")
    body = part("Body", [sph((0, 0, 0), 0.24, "Steel", 2, (1, 1, 0.6)),
                         cyl((0, -0.10, -0.02), (0, -0.25, -0.02), 0.13, 0.13, "White", 12),
                         cyl((0, -0.25, -0.02), (0, -0.265, -0.02), 0.10, 0.10, "RobotEye", 12),
                         box((0, 0, 0.0), (0.95, 0.06, 0.04), "SteelDark"), box((0, 0, 0.0), (0.06, 0.95, 0.04), "SteelDark")],
                (0, 0, 0))
    child(body, rt)
    for i, (x, y) in enumerate(((0.46, 0), (-0.46, 0), (0, 0.46), (0, -0.46))):
        rotor = part("Rotor%d" % (i + 1), [cyl((x, y, 0.05), (x, y, 0.075), 0.20, 0.20, "Warn", 8),
                                           cyl((x, y, 0.0), (x, y, 0.06), 0.03, 0.03, "SteelDark", 6)], (x, y, 0.05))
        child(rotor, body)
    export("robot_flyer")

def robot_boss():
    reset(); rt = root("Robot")
    body = part("Body", [box((0, 0.1, 1.05), (1.5, 1.5, 1.1), "BossBody"),
                         box((0, 0.1, 0.42), (1.7, 1.75, 0.34), "SteelDark"),
                         box((0, -0.66, 1.28), (0.9, 0.05, 0.16), "RobotEye"),
                         box((0, 0.1, 1.66), (1.2, 1.2, 0.14), "Warn"),
                         sph((0, 0.25, 1.95), 0.42, "Steel", 2, (1, 1, 0.75))], (0, 0, 0))
    cannon = part("Cannon", [cyl((0, -0.55, 0.98), (0, -1.45, 1.10), 0.26, 0.32, "Steel", 10),
                             cyl((0, -1.45, 1.10), (0, -1.50, 1.105), 0.24, 0.24, "Black", 10)], (0, -0.55, 0.98))
    child(body, rt); child(cannon, body)
    for i, (x, y) in enumerate(((0.92, -0.45), (-0.92, -0.45), (0.92, 0.65), (-0.92, 0.65))):
        w = part("Wheel%d" % (i + 1), [cyl((x - 0.11, y, 0.36), (x + 0.11, y, 0.36), 0.36, 0.36, "Tyre", 12),
                                       cyl((x - 0.12, y, 0.36), (x + 0.12, y, 0.36), 0.14, 0.14, "Warn", 8)], (x, y, 0.36))
        child(w, rt)
    export("robot_boss")

# ------------------------------------------------------------------ Escenario
def trophy_parts(z0, s=1.0):
    return [cyl((0, 0, z0), (0, 0, z0 + 0.12 * s), 0.34 * s, 0.30 * s, "Gold", 10),
            cyl((0, 0, z0 + 0.12 * s), (0, 0, z0 + 0.5 * s), 0.07 * s, 0.07 * s, "Gold", 8),
            cyl((0, 0, z0 + 0.5 * s), (0, 0, z0 + 1.15 * s), 0.16 * s, 0.46 * s, "Gold", 10),
            torus((0.52 * s, 0, z0 + 0.9 * s), 0.16 * s, 0.04 * s, "Gold", (0, 1, 0), 10),
            torus((-0.52 * s, 0, z0 + 0.9 * s), 0.16 * s, 0.04 * s, "Gold", (0, 1, 0), 10)]

def base():
    """El club: una grada con marquesina y, delante, la copa que hay que defender. Mira hacia -Y."""
    reset(); rt = root("Base")
    b = [box((0, 1.6, 1.3), (7.0, 2.2, 2.6), "Wall"),
         box((0, 1.5, 2.75), (7.6, 2.8, 0.30), "Roof"),
         box((0, 0.46, 0.9), (1.5, 0.10, 1.8), "Roof"),
         box((-2.2, 0.46, 1.5), (1.4, 0.08, 0.8), "SteelDark"), box((2.2, 0.46, 1.5), (1.4, 0.08, 0.8), "SteelDark"),
         cyl((3.2, 1.5, 2.9), (3.2, 1.5, 4.6), 0.05, 0.05, "SteelDark", 6), box((3.55, 1.5, 4.3), (0.7, 0.04, 0.45), "Kit"),
         cyl((0, -1.2, 0.0), (0, -1.2, 0.5), 1.05, 0.9, "Wall", 12), cyl((0, -1.2, 0.5), (0, -1.2, 0.62), 0.8, 0.8, "Roof", 12)]
    building = part("Building", b, (0, 0, 0))
    child(building, rt)
    cup = part("Cup", [o for o in trophy_parts(0.62, 1.25)], (0, 0, 0.62))
    cup.location.y = -1.2
    child(cup, rt)
    export("base")

def field():
    """Césped a franjas, pista por la que llegan los robots (eje X en Blender), gradas y vallas al fondo."""
    reset(); rt = root("Field")
    objs = []
    for i in range(-10, 10):   # franjas de césped de 3 m (X de -30 a 30)
        objs.append(box((i * 3 + 1.5, 0, -0.05), (3, 44, 0.1), "Grass" if i % 2 == 0 else "Grass2"))
    objs.append(box((0, 0, 0.005), (60, 5.2, 0.02), "Track"))
    for y in (-2.6, -0.87, 0.87, 2.6):
        objs.append(box((0, y, 0.02), (60, 0.07, 0.02), "Line"))
    ground = part("Ground", objs, (0, 0, 0))
    child(ground, rt)
    st = []
    for row in range(4):       # grada al fondo (+Y)
        st.append(box((0, 9.0 + row * 1.0, 0.35 + row * 0.55), (44, 1.0, 0.7 + row * 1.1), "Concrete"))
        for k in range(-10, 11):
            st.append(box((k * 2.0, 8.85 + row * 1.0, 0.9 + row * 1.1), (1.5, 0.45, 0.28), "Seat1" if (k + row) % 2 == 0 else "Seat2"))
    for k in range(-11, 12):   # vallas publicitarias
        st.append(box((k * 2.0, 7.3, 0.45), (1.9, 0.12, 0.9), ("Wall", "Roof", "Warn", "Kit")[k % 4]))
    for x in (-18, 18):        # focos
        st.append(cyl((x, 12.6, 0), (x, 12.6, 9), 0.18, 0.12, "SteelDark", 6))
        st.append(box((x, 12.4, 9.3), (2.4, 0.3, 1.2), "Wall"))
    stands = part("Stands", st, (0, 0, 0))
    child(stands, rt)
    export("field")

def slot():
    reset(); rt = root("Slot")
    p = part("Pad", [cyl((0, 0, 0), (0, 0, 0.14), 0.78, 0.70, "Podium", 14),
                     cyl((0, 0, 0.14), (0, 0, 0.165), 0.62, 0.62, "PodiumRing", 14),
                     cyl((0, 0, 0.165), (0, 0, 0.18), 0.50, 0.50, "Podium", 14)], (0, 0, 0))
    child(p, rt); export("slot")

def prims():
    reset(); part("Cube", [box((0, 0, 0), (1, 1, 1), "White")], (0, 0, 0)); export("prim_cube")
    reset(); part("Sphere", [sph((0, 0, 0), 0.5, "White", 2)], (0, 0, 0)); export("prim_sphere")
    reset(); part("Disc", [cyl((0, 0, 0), (0, 0, 0.02), 1, 1, "White", 28)], (0, 0, 0)); export("prim_disc")
    reset(); part("Coin", [cyl((0, -0.03, 0), (0, 0.03, 0), 0.22, 0.22, "Gold", 12)], (0, 0, 0)); export("prim_coin")

for s in ("soccer", "tennis", "basket", "bowling", "pingpong"): athlete(s)
balls(); robot_basic(); robot_runner(); robot_armored(); robot_flyer(); robot_boss()
base(); field(); slot(); prims()
print("OK", len(os.listdir(OUT)), "modelos en", OUT)
