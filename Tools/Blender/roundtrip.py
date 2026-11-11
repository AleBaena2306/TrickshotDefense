# Reimporta un FBX en Blender e imprime el centro de cada pieza (uso: python roundtrip.py modelo.fbx)
import bpy, sys, mathutils
path=sys.argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
for o in sorted(bpy.context.scene.objects, key=lambda o:o.name):
    if o.type=='MESH':
        c=sum((o.matrix_world @ mathutils.Vector(v) for v in o.bound_box), mathutils.Vector())/8
        print(f"{o.name:8s} parent={o.parent.name if o.parent else '-':8s} center=({c.x:+.2f},{c.y:+.2f},{c.z:+.2f}) loc={tuple(round(x,2) for x in o.location)} rot={tuple(round(x,1) for x in o.rotation_euler)}")
