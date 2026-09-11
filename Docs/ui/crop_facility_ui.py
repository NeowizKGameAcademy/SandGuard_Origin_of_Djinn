import bpy, sys, os, math
argv = sys.argv[sys.argv.index("--")+1:]
src, out = argv[0], argv[1]
img = bpy.data.images.load(src)
W, H = img.size
px = list(img.pixels)  # RGBA float, bottom-left origin
def crop(name, cx, cy_top, size, circle=None):
    # cx, cy_top are in top-left-origin pixel coords (as seen in the image viewer)
    cy = H - cy_top
    half = size // 2
    x0, y0 = int(cx - half), int(cy - half)
    new = bpy.data.images.new(name, size, size, alpha=True)
    buf = [0.0] * (size * size * 4)
    for y in range(size):
        for x in range(size):
            sx, sy = x0 + x, y0 + y
            i = (sy * W + sx) * 4; o = (y * size + x) * 4
            r, g, b, a = px[i], px[i+1], px[i+2], px[i+3]
            if circle is not None:
                d = math.hypot(x - half + .5, y - half + .5)
                a = a * max(0.0, min(1.0, circle + 1.0 - d))  # 1px soft edge
            buf[o:o+4] = [r, g, b, a]
    new.pixels = buf
    new.filepath_raw = os.path.join(out, name + ".png"); new.file_format = 'PNG'; new.save()
    print("CROP", name, x0, y0, size)
# Number circles, top row (black). Centers measured on the 1536x1024 sheet.
for n, cx in enumerate([883, 969, 1057, 1146, 1233], start=1):
    crop("UI_Number_%d" % n, cx, 378, 80, circle=36)
# Facility icons, top row.
for name, cx in [("Turret", 102), ("Bow", 245), ("Magic", 388), ("Cobra", 537), ("Crystal", 680)]:
    crop("UI_Icon_" + name, cx, 407, 132)
# Selected-ring (cyan circle, second row) and a plain dark disc for tiles.
crop("UI_Ring_Cyan", 130, 245, 176, circle=84)
crop("UI_Disc_Dark", 90, 82, 152, circle=72)
print("DONE")
