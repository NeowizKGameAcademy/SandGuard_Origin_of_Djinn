"""Make compact lossless-in-layout inputs and an upload manifest."""
from pathlib import Path
from PIL import Image
import hashlib, json

out = Path(__file__).resolve().parent
for stem in ['R01-look-start', 'R01-look-end']:
    im = Image.open(out / (stem + '.png')).convert('RGB')
    im.resize((1280, 720), Image.Resampling.LANCZOS).save(out / (stem + '.jpg'), quality=92, subsampling=0)
files=[]
for name in ['R01-motion-reference.mp4','R01-look-start.jpg','R01-look-end.jpg']:
    p=out/name
    files.append({'filename':name,'path':str(p),'size_bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'type':'video' if p.suffix=='.mp4' else 'image'})
(out/'transfer-manifest.json').write_text(json.dumps(files,indent=2),encoding='utf-8')
print(json.dumps(files))
