"""Post-pass for out/light.png: the voxel-marched key/fill visibility is
blotchy on morphed faces, so blur key/fill wide and AO lightly."""
from PIL import Image, ImageFilter
im = Image.open('out/light.png')
r, g, b, a = im.split()
Image.merge('RGBA', (r.filter(ImageFilter.GaussianBlur(2.5)), g.filter(ImageFilter.GaussianBlur(7)),
                     b.filter(ImageFilter.GaussianBlur(7)), a)).save('out/light.png')
print('softened light.png')
