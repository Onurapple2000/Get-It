"""
Blender Python Script — Deco Objects for GET_IT Unity Project
Run: blender --background --python create_deco_objects.py
"""

import bpy
import bmesh
import math
import os

OUTPUT_DIR = "/Users/onur/Documents/GET_IT_Unity/Assets/Models/DecoObjects"
os.makedirs(OUTPUT_DIR, exist_ok=True)


# ─── HELPERS ──────────────────────────────────────────────────────────────────

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.outliner.orphans_purge(do_recursive=True)


def mat(name, color, roughness=0.80, metallic=0.0, emission_color=None, emission_strength=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name=name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out  = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs['Base Color'].default_value  = (*color, 1.0)
    bsdf.inputs['Roughness'].default_value   = roughness
    bsdf.inputs['Metallic'].default_value    = metallic
    if emission_color:
        bsdf.inputs['Emission Color'].default_value    = (*emission_color, 1.0)
        bsdf.inputs['Emission Strength'].default_value = emission_strength
    return m


def assign(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)


def bevel(obj, width=0.015, segs=2):
    mod = obj.modifiers.new("Bevel", 'BEVEL')
    mod.width          = width
    mod.segments       = segs
    mod.limit_method   = 'ANGLE'
    mod.angle_limit    = math.radians(60)
    apply_mod(obj, mod.name)


def apply_mod(obj, name):
    ctx = bpy.context.copy()
    ctx['object'] = obj
    with bpy.context.temp_override(object=obj):
        bpy.ops.object.modifier_apply(modifier=name)


def cone(r_bot, r_top, depth, loc, verts=24):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r_bot, radius2=r_top,
        depth=depth, location=loc)
    return bpy.context.active_object


def cyl(radius, depth, loc, verts=24):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=loc)
    return bpy.context.active_object


def sphere(radius, loc, segs=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, segments=segs, ring_count=rings, location=loc)
    return bpy.context.active_object


def cube(size, loc):
    bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    return bpy.context.active_object


def torus(major_r, minor_r, loc, major_segs=24, minor_segs=8):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_r, minor_radius=minor_r,
        major_segments=major_segs, minor_segments=minor_segs,
        location=loc)
    return bpy.context.active_object


def join_all():
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    return bpy.context.active_object


def export(name):
    obj = bpy.context.active_object
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    path = os.path.join(OUTPUT_DIR, f"{name}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        colors_type='SRGB',
    )
    print(f"  → Exported: {path}")


# ─── 1. FLOWER POT ────────────────────────────────────────────────────────────
def make_flower_pot():
    clear_scene()
    m_terra = mat("Terra",  (0.76, 0.36, 0.16), roughness=0.88)
    m_soil  = mat("Soil",   (0.18, 0.10, 0.04), roughness=0.96)
    m_green = mat("FPGreen",(0.16, 0.52, 0.10), roughness=0.82)
    m_pink  = mat("FPPink", (0.95, 0.42, 0.65), roughness=0.72)
    m_yell  = mat("FPYell", (0.98, 0.88, 0.10), roughness=0.70)

    # Pot body — taper: narrow bottom, wide top
    pot = cone(0.19, 0.28, 0.32, (0, 0, 0.16), verts=24)
    assign(pot, m_terra)
    bevel(pot, 0.012, 2)

    # Rim torus
    r = torus(0.295, 0.030, (0, 0, 0.325), 24, 8)
    assign(r, m_terra)

    # Soil disc
    s = cyl(0.26, 0.04, (0, 0, 0.335), 24)
    assign(s, m_soil)

    # Bush (main)
    b = sphere(0.24, (0, 0, 0.590), 18, 12)
    assign(b, m_green)

    # Small puff on top
    b2 = sphere(0.14, (0, 0, 0.790), 12, 8)
    assign(b2, m_green)

    # 5 pink flowers arranged in ring
    for i in range(5):
        a = math.radians(i * 72)
        x, y = 0.15 * math.cos(a), 0.15 * math.sin(a)
        f = sphere(0.065, (x, y, 0.75), 8, 6)
        assign(f, m_pink)
        # yellow center
        fc = sphere(0.030, (x, y, 0.80), 6, 4)
        assign(fc, m_yell)

    j = join_all()
    j.name = "FlowerPot"
    export("FlowerPot")


# ─── 2. SMALL BARREL ─────────────────────────────────────────────────────────
def make_small_barrel():
    clear_scene()
    m_wood  = mat("BrlWood",  (0.42, 0.22, 0.08), roughness=0.92)
    m_metal = mat("BrlMetal", (0.30, 0.30, 0.38), roughness=0.35, metallic=0.85)

    # Barrel body with bulge via bmesh
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.24, depth=0.42, location=(0,0,0.21))
    barrel = bpy.context.active_object
    assign(barrel, m_wood)

    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(barrel.data)
    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        t = v.co.z / 0.21          # -1..+1 (local space center at 0)
        bulge = 1.0 + 0.14 * (1.0 - t * t)
        v.co.x *= bulge
        v.co.y *= bulge
    bmesh.update_edit_mesh(barrel.data)
    bpy.ops.object.mode_set(mode='OBJECT')

    # Smooth shading
    bpy.ops.object.shade_smooth()

    # 3 Metal hoops
    for z in [0.08, 0.21, 0.34]:
        h = torus(0.268, 0.022, (0, 0, z), 24, 6)
        assign(h, m_metal)

    # Top & bottom caps
    for z in [0.01, 0.40]:
        c = cyl(0.230, 0.018, (0, 0, z), 24)
        assign(c, m_wood)

    j = join_all()
    j.name = "SmallBarrel"
    export("SmallBarrel")


# ─── 3. SMALL CRATE ──────────────────────────────────────────────────────────
def make_small_crate():
    clear_scene()
    m_light = mat("CrateLight", (0.70, 0.50, 0.26), roughness=0.88)
    m_dark  = mat("CrateDark",  (0.38, 0.22, 0.08), roughness=0.93)

    # Main box
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.20))
    box = bpy.context.active_object
    box.scale = (0.40, 0.40, 0.40)
    bpy.ops.object.transform_apply(scale=True)
    assign(box, m_light)
    bevel(box, 0.012, 2)

    # Horizontal planks on front/back faces
    for sign in [-1, 1]:
        for z in [0.07, 0.20, 0.33]:
            p = cube(1.0, (0, sign * 0.202, z))
            p.scale = (0.385, 0.004, 0.040)
            bpy.ops.object.transform_apply(scale=True)
            assign(p, m_dark)

    # Horizontal planks on left/right faces
    for sign in [-1, 1]:
        for z in [0.07, 0.20, 0.33]:
            p = cube(1.0, (sign * 0.202, 0, z))
            p.scale = (0.004, 0.385, 0.040)
            bpy.ops.object.transform_apply(scale=True)
            assign(p, m_dark)

    # Vertical corner strips
    for sx in [-1, 1]:
        for sy in [-1, 1]:
            s = cube(1.0, (sx * 0.195, sy * 0.195, 0.20))
            s.scale = (0.012, 0.012, 0.20)
            bpy.ops.object.transform_apply(scale=True)
            assign(s, m_dark)

    # Top/bottom edge frame
    for z in [0.01, 0.39]:
        for sx in [-1, 1]:
            s = cube(1.0, (sx * 0.195, 0, z))
            s.scale = (0.012, 0.39, 0.012)
            bpy.ops.object.transform_apply(scale=True)
            assign(s, m_dark)
        for sy in [-1, 1]:
            s = cube(1.0, (0, sy * 0.195, z))
            s.scale = (0.39, 0.012, 0.012)
            bpy.ops.object.transform_apply(scale=True)
            assign(s, m_dark)

    j = join_all()
    j.name = "SmallCrate"
    export("SmallCrate")


# ─── 4. BENCH ────────────────────────────────────────────────────────────────
def make_bench():
    clear_scene()
    m_wood  = mat("BenchWood",  (0.62, 0.42, 0.20), roughness=0.87)
    m_iron  = mat("BenchIron",  (0.18, 0.18, 0.22), roughness=0.50, metallic=0.75)

    # Seat — 4 planks
    for i in range(4):
        y = -0.145 + i * 0.095
        p = cube(1.0, (0, y, 0.46))
        p.scale = (0.62, 0.042, 0.028)
        bpy.ops.object.transform_apply(scale=True)
        bevel(p, 0.006, 2)
        assign(p, m_wood)

    # Backrest — 3 planks
    for i in range(3):
        z = 0.540 + i * 0.085
        p = cube(1.0, (0, -0.195, z))
        p.scale = (0.62, 0.022, 0.030)
        bpy.ops.object.transform_apply(scale=True)
        bevel(p, 0.005, 1)
        assign(p, m_wood)

    # Cast-iron side frames (2 sides)
    for sx in [-0.58, 0.58]:
        # Front leg (angled)
        fl = cyl(0.022, 0.52, (sx, 0.10, 0.26), verts=10)
        fl.rotation_euler = (math.radians(-8), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)
        assign(fl, m_iron)

        # Back leg (angled)
        bl = cyl(0.022, 0.78, (sx, -0.18, 0.42), verts=10)
        bl.rotation_euler = (math.radians(12), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)
        assign(bl, m_iron)

        # Armrest
        arm = cube(1.0, (sx, -0.045, 0.72))
        arm.scale = (0.040, 0.155, 0.022)
        bpy.ops.object.transform_apply(scale=True)
        bevel(arm, 0.010, 2)
        assign(arm, m_wood)

        # Side scroll decoration (small torus)
        deco = torus(0.045, 0.012, (sx, 0.14, 0.08), 12, 6)
        assign(deco, m_iron)

    # Cross brace
    br = cyl(0.016, 1.16, (0, -0.02, 0.18), verts=8)
    br.rotation_euler = (0, math.radians(90), 0)
    bpy.ops.object.transform_apply(rotation=True)
    assign(br, m_iron)

    j = join_all()
    j.name = "Bench"
    export("Bench")


# ─── 5. STREET LAMP ──────────────────────────────────────────────────────────
def make_street_lamp():
    clear_scene()
    m_iron  = mat("LampIron",  (0.10, 0.10, 0.14), roughness=0.55, metallic=0.80)
    m_brass = mat("LampBrass", (0.72, 0.54, 0.08), roughness=0.38, metallic=0.90)
    m_glass = mat("LampGlass", (1.00, 0.94, 0.70), roughness=0.04,
                  emission_color=(1.0, 0.92, 0.65), emission_strength=3.0)

    # Octagonal base plate
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.20, depth=0.06, location=(0,0,0.03))
    base = bpy.context.active_object
    assign(base, m_iron)
    bevel(base, 0.010, 2)

    # Lower post (tapered, wider)
    post1 = cone(0.085, 0.058, 1.80, (0, 0, 0.96), verts=12)
    assign(post1, m_iron)

    # Decorative belly collar
    belly = torus(0.095, 0.028, (0, 0, 0.85), 12, 8)
    assign(belly, m_brass)

    # Upper post (slender)
    post2 = cyl(0.038, 1.30, (0, 0, 2.51), verts=12)
    assign(post2, m_iron)

    # Top collar
    tc = torus(0.065, 0.020, (0, 0, 3.16), 12, 6)
    assign(tc, m_brass)

    # Curved arm — 4 segments forming arc
    arm_pts = [
        ((0.10, 0, 3.18), (math.radians(80), 0, 0), 0.26),
        ((0.22, 0, 3.29), (math.radians(55), 0, 0), 0.24),
        ((0.35, 0, 3.36), (math.radians(25), 0, 0), 0.20),
        ((0.46, 0, 3.38), (math.radians(5),  0, 0), 0.16),
    ]
    for pos, rot, length in arm_pts:
        seg = cyl(0.026, length, pos, verts=10)
        seg.rotation_euler = rot
        bpy.ops.object.transform_apply(rotation=True)
        assign(seg, m_iron)

    # Lamp head (hexagonal)
    head = cyl(0.175, 0.240, (0.54, 0, 3.35), verts=6)
    assign(head, m_iron)

    # Brass top cap
    cap = cone(0.190, 0.060, 0.180, (0.54, 0, 3.49), verts=6)
    assign(cap, m_brass)

    # Brass bottom rim
    rim = torus(0.180, 0.022, (0.54, 0, 3.23), 6, 6)
    assign(rim, m_brass)

    # Glass globe (emission)
    globe = sphere(0.115, (0.54, 0, 3.35), 14, 10)
    assign(globe, m_glass)

    # Finial ball at top of post
    fin = sphere(0.048, (0, 0, 3.18), 8, 6)
    assign(fin, m_brass)

    j = join_all()
    j.name = "StreetLamp"
    export("StreetLamp")


# ─── 6. TREE ─────────────────────────────────────────────────────────────────
def make_tree():
    clear_scene()
    m_bark  = mat("TreeBark",  (0.32, 0.18, 0.06), roughness=0.95)
    m_leaf1 = mat("TreeLeaf1", (0.12, 0.50, 0.10), roughness=0.85)
    m_leaf2 = mat("TreeLeaf2", (0.18, 0.60, 0.14), roughness=0.82)
    m_leaf3 = mat("TreeLeaf3", (0.08, 0.38, 0.07), roughness=0.88)

    # Root flare
    rf = cone(0.48, 0.26, 0.30, (0, 0, 0.15), verts=16)
    assign(rf, m_bark)
    bpy.ops.object.shade_smooth()

    # Trunk (tapered)
    tr = cone(0.26, 0.14, 1.70, (0, 0, 1.05), verts=16)
    assign(tr, m_bark)
    bpy.ops.object.shade_smooth()

    # Upper trunk fork
    uf = cone(0.14, 0.07, 0.65, (0, 0, 2.23), verts=12)
    assign(uf, m_bark)

    # Branch stubs (4 directions)
    for i in range(4):
        a = math.radians(i * 90 + 22)
        bx, by = 0.30 * math.cos(a), 0.30 * math.sin(a)
        br = cone(0.07, 0.03, 0.55, (bx, by, 1.80), verts=8)
        br.rotation_euler = (math.radians(35) * (1 if by > 0 else -1),
                             math.radians(35) * (1 if bx > 0 else -1), a)
        bpy.ops.object.transform_apply(rotation=True)
        assign(br, m_bark)

    # Main canopy (layered spheres)
    canopy = [
        # (pos,              radius, mat)
        ((  0.00,  0.00, 2.90), 0.92, m_leaf1),
        ((  0.70,  0.35, 2.55), 0.70, m_leaf2),
        (( -0.70, -0.35, 2.55), 0.70, m_leaf2),
        ((  0.45, -0.65, 2.58), 0.65, m_leaf1),
        (( -0.45,  0.65, 2.58), 0.65, m_leaf1),
        ((  0.00,  0.70, 2.62), 0.62, m_leaf3),
        ((  0.00, -0.70, 2.62), 0.62, m_leaf3),
        ((  0.72, -0.10, 2.72), 0.58, m_leaf2),
        (( -0.72,  0.10, 2.72), 0.58, m_leaf2),
        # Second layer (higher)
        ((  0.38,  0.38, 3.30), 0.55, m_leaf1),
        (( -0.38, -0.38, 3.30), 0.52, m_leaf3),
        ((  0.00,  0.42, 3.35), 0.50, m_leaf2),
        (( -0.42,  0.00, 3.32), 0.48, m_leaf1),
        # Top tuft
        ((  0.15,  0.10, 3.72), 0.38, m_leaf2),
        (( -0.15, -0.10, 3.68), 0.35, m_leaf3),
    ]
    for pos, r, m in canopy:
        s = sphere(r, pos, 16, 10)
        assign(s, m)
        bpy.ops.object.shade_smooth()

    j = join_all()
    j.name = "Tree"
    export("Tree")


# ─── 7. LARGE PLANTER ────────────────────────────────────────────────────────
def make_large_planter():
    clear_scene()
    m_clay   = mat("PlClay",   (0.70, 0.26, 0.12), roughness=0.90)
    m_stone  = mat("PlStone",  (0.58, 0.54, 0.48), roughness=0.92)
    m_soil   = mat("PlSoil",   (0.15, 0.09, 0.04), roughness=0.96)
    m_leaf   = mat("PlLeaf",   (0.14, 0.50, 0.11), roughness=0.83)
    m_leaf2  = mat("PlLeaf2",  (0.20, 0.60, 0.16), roughness=0.80)
    m_flower = mat("PlFlower", (0.95, 0.82, 0.08), roughness=0.72)
    m_flwrR  = mat("PlFlwrR",  (0.92, 0.25, 0.35), roughness=0.72)

    # Pot body (frustum — wide top)
    pot = cone(0.56, 0.84, 0.96, (0, 0, 0.48), verts=24)
    assign(pot, m_clay)
    bevel(pot, 0.018, 2)
    bpy.ops.object.shade_smooth()

    # Decorative top rim (torus)
    rim = torus(0.86, 0.060, (0, 0, 0.965), 24, 10)
    assign(rim, m_stone)

    # Mid band decoration
    band = torus(0.72, 0.038, (0, 0, 0.480), 24, 8)
    assign(band, m_stone)

    # Bottom base disc
    base = cyl(0.60, 0.055, (0, 0, 0.028), 24)
    assign(base, m_stone)

    # Soil fill
    soil = cyl(0.80, 0.055, (0, 0, 0.988), 24)
    assign(soil, m_soil)

    # Central large bush
    cb = sphere(0.56, (0, 0, 1.52), 18, 12)
    assign(cb, m_leaf)
    bpy.ops.object.shade_smooth()

    # Surrounding bushes
    surrounding = [
        ((0.50,  0.00, 1.26), 0.36, m_leaf2),
        ((-0.50, 0.00, 1.26), 0.36, m_leaf),
        ((0.00,  0.50, 1.26), 0.34, m_leaf2),
        ((0.00, -0.50, 1.26), 0.34, m_leaf),
        ((0.36,  0.36, 1.30), 0.28, m_leaf),
        ((-0.36,-0.36, 1.30), 0.28, m_leaf2),
        ((0.36, -0.36, 1.30), 0.26, m_leaf2),
        ((-0.36, 0.36, 1.30), 0.26, m_leaf),
    ]
    for pos, r, m in surrounding:
        b = sphere(r, pos, 12, 8)
        assign(b, m)
        bpy.ops.object.shade_smooth()

    # Yellow flowers on top (ring of 6)
    for i in range(6):
        a = math.radians(i * 60)
        x, y = 0.28 * math.cos(a), 0.28 * math.sin(a)
        f = sphere(0.062, (x, y, 2.02), 8, 6)
        assign(f, m_flower)

    # Red accent flowers (3 taller)
    for i in range(3):
        a = math.radians(i * 120 + 30)
        x, y = 0.18 * math.cos(a), 0.18 * math.sin(a)
        f = sphere(0.055, (x, y, 2.09), 8, 6)
        assign(f, m_flwrR)

    j = join_all()
    j.name = "LargePlanter"
    export("LargePlanter")


# ─── MAIN ─────────────────────────────────────────────────────────────────────
print("\n=== Deco Object Creator ===")
make_flower_pot();    print("[1/7] FlowerPot done")
make_small_barrel();  print("[2/7] SmallBarrel done")
make_small_crate();   print("[3/7] SmallCrate done")
make_bench();         print("[4/7] Bench done")
make_street_lamp();   print("[5/7] StreetLamp done")
make_tree();          print("[6/7] Tree done")
make_large_planter(); print("[7/7] LargePlanter done")
print("=== All exported to:", OUTPUT_DIR, "===\n")
