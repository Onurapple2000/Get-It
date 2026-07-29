# -*- coding: utf-8 -*-
# =====================================================================
#  Meshy -> Low-Poly  (ucretsiz "repair + retexture")  Blender 4.x/5.x
#
#  IKI MOD var (asagidaki MODE ayarindan sec):
#
#  MODE = "DECIMATE"  (ONERILEN - temiz)
#     - Orijinali dogrudan decimate eder.
#     - Orijinal UV + texture AYNEN korunur (bake YOK -> bake hatasi YOK).
#     - Delikleri Fill Holes ile kapatir.
#     - Ince/organik modeller (yaprak, sac, kumas) icin en iyisi.
#
#  MODE = "REMESH_BAKE"
#     - Voxel remesh (delikleri hacimsel kapatir) + texture bake.
#     - Cok delikli / karmasik ic yapili modeller icin.
#     - Ince yuzeylerde uslupsal bozulma/uucgen artefakt olabilir.
# =====================================================================

import bpy
import os

# ============================ AYARLAR ================================
INPUT_GLB   = "/Users/onur/Documents/GET_IT_Unity/BlenderTools/input/model.glb"
OUTPUT_GLB  = "/Users/onur/Documents/GET_IT_Unity/BlenderTools/output/model_lowpoly.glb"

MODE = "REMESH_BAKE"     # "REMESH_BAKE" (Meshy modelleri icin DOGRU secim) ya da "DECIMATE"

TARGET_TRIS = 4500       # hedef ucgen sayisi (3000-4000)
FILL_HOLES  = False      # DECIMATE modunda delik kapatma (mesh'i bozabilir, kapali birak)

# --- Sadece REMESH_BAKE modu icin ---
IMAGE_SIZE   = 2048      # bake texture cozunurlugu
VOXEL_DETAIL = 220       # remesh detayi (150-300). buyuk = yuzey orijinale daha yakin, bake temiz
CAGE_FACTOR  = 0.15      # bake kafes mesafesi. ince kenar acigi varsa ARTIR, oyukta yanlis renk varsa AZALT
BAKE_MARGIN  = 16        # texture kenar tasmasi (piksel)
BAKE_SAMPLES = 16        # bake ornekleme (16 temiz, dusuk = daha hizli)
# ====================================================================


def log(m): print("[MESHY-BAKE] " + str(m))


def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.textures):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def import_glb(path):
    if not os.path.isfile(path):
        raise FileNotFoundError("INPUT_GLB bulunamadi: " + path)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
    if not imported:
        raise RuntimeError("GLB icinde mesh bulunamadi.")
    return imported


def join_objects(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    return obj


def max_dimension(obj):
    return max(obj.dimensions.x, obj.dimensions.y, obj.dimensions.z)


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def duplicate(obj, name):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.duplicate()
    low = bpy.context.view_layer.objects.active
    low.name = name
    return low


def fill_holes(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.fill_holes(sides=0)   # her boyuttaki deligi kapat
    bpy.ops.object.mode_set(mode='OBJECT')


def decimate(obj, target_tris):
    current = tri_count(obj)
    if current > target_tris:
        dec = obj.modifiers.new(name="Decimate", type='DECIMATE')
        dec.decimate_type = 'COLLAPSE'
        dec.ratio = max(0.01, min(1.0, float(target_tris) / float(current)))
        bpy.ops.object.modifier_apply(modifier=dec.name)


# ---------------- DECIMATE modu (temiz, orijinal texture korunur) ----------
def run_decimate_mode(high):
    low = duplicate(high, "LOWPOLY")
    if FILL_HOLES:
        fill_holes(low)
        log("Delikler kapatildi.")
    decimate(low, TARGET_TRIS)
    log("Decimate sonrasi ucgen: {}".format(tri_count(low)))
    return low, None   # orijinal materyaller/texture aynen korunur


# ---------------- REMESH_BAKE modu ----------------------------------------
def run_remesh_bake_mode(high):
    low = duplicate(high, "LOWPOLY")
    max_dim = max_dimension(low)
    rem = low.modifiers.new(name="Remesh", type='REMESH')
    rem.mode = 'VOXEL'
    rem.voxel_size = max_dim / float(VOXEL_DETAIL)
    rem.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=rem.name)
    log("Remesh sonrasi ucgen: {}".format(tri_count(low)))
    decimate(low, TARGET_TRIS)
    log("Decimate sonrasi ucgen: {}".format(tri_count(low)))

    # yeni UV
    bpy.ops.object.select_all(action='DESELECT')
    low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')

    # bake materyali
    low.data.materials.clear()
    mat = bpy.data.materials.new(name="LOWPOLY_MAT"); mat.use_nodes = True
    low.data.materials.append(mat)
    nodes = mat.node_tree.nodes; links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    img = bpy.data.images.new("BakedTexture", width=IMAGE_SIZE, height=IMAGE_SIZE)
    tex = nodes.new(type='ShaderNodeTexImage'); tex.image = img; tex.location = (-400, 0)
    if bsdf:
        links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    for n in nodes:
        n.select = False
    tex.select = True; nodes.active = tex

    # bake
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = BAKE_SAMPLES
    cage = CAGE_FACTOR * max_dim
    b = scene.render.bake
    b.use_selected_to_active = True
    b.cage_extrusion = cage
    b.max_ray_distance = cage * 2.0
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True); low.select_set(True)
    bpy.context.view_layer.objects.active = low
    log("Bake basliyor...")
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'},
                        use_selected_to_active=True,
                        cage_extrusion=cage, margin=BAKE_MARGIN)
    log("Bake tamam.")
    return low, img


def export_glb(low, img, out_path):
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    if img is not None:
        img.pack()
    bpy.ops.object.select_all(action='DESELECT')
    low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.export_scene.gltf(filepath=out_path, export_format='GLB',
                              use_selection=True, export_image_format='AUTO',
                              export_yup=True)
    log("Export edildi: " + out_path)


def main():
    clean_scene()
    imported = import_glb(INPUT_GLB)
    high = join_objects(imported, "HIGHPOLY")
    log("Orijinal ucgen: {}".format(tri_count(high)))

    if MODE == "DECIMATE":
        low, img = run_decimate_mode(high)
    elif MODE == "REMESH_BAKE":
        low, img = run_remesh_bake_mode(high)
    else:
        raise ValueError("MODE 'DECIMATE' ya da 'REMESH_BAKE' olmali.")

    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True); bpy.ops.object.delete()

    export_glb(low, img, OUTPUT_GLB)
    log("BITTI. Mod = {}".format(MODE))


if __name__ == "__main__":
    main()
