# Turns a Canva logo on white into a transparent, square 1024px game logo.
# Usage: python3 tools/brand_cutout.py in.png game/assets/teams/<id>/logo.png
import sys, numpy as np
from PIL import Image
from scipy import ndimage
src, dst = sys.argv[1], sys.argv[2]
im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
mn = im.min(axis=2)
white = mn > 232
# close small gaps in outlines so white inside rings/balls stays opaque
logo = ndimage.binary_closing(~white, structure=np.ones((3, 3)), iterations=18)
filled = ndimage.binary_fill_holes(logo)
bg = white & ~filled
# soft edge: pixels within 3 px of bg get alpha from how white they are
near = ndimage.binary_dilation(bg, iterations=3) & ~bg
alpha = np.ones(mn.shape, np.float32)
alpha[bg] = 0
a_edge = np.clip((255 - mn) / (255 - 150), 0, 1)
alpha[near] = a_edge[near]
rgb = im.copy()
a3 = np.maximum(alpha[..., None], 1e-3)
rgb[near] = np.clip((im[near] - (1 - alpha[near][:, None]) * 255) / a3[near], 0, 255)
out = np.dstack([rgb, alpha * 255]).astype(np.uint8)
img = Image.fromarray(out, 'RGBA')
bbox = img.getbbox()
img = img.crop(bbox)
w, h = img.size
side = int(max(w, h) * 1.04)
canvas = Image.new('RGBA', (side, side), (0, 0, 0, 0))
canvas.paste(img, ((side - w) // 2, (side - h) // 2))
canvas = canvas.resize((1024, 1024), Image.LANCZOS)
canvas.save(dst, optimize=True)
print(dst, bbox, canvas.size)
