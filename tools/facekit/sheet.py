import json,sys,subprocess
from PIL import Image
specs=json.load(open(sys.argv[1]))
subprocess.run([sys.executable,'shot.py',json.dumps(specs),'out/s'],check=True)
n=len(specs); cols=min(n,4); rows=(n+cols-1)//cols
c=Image.new('RGB',(512*cols,640*rows))
for i in range(n): c.paste(Image.open(f'out/s_{i}.png'),(512*(i%cols),640*(i//cols)))
c.save(sys.argv[2])
