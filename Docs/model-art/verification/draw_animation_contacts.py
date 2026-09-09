import json,math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]/'animation-selection-v1'
rows=json.loads((ROOT/'library-audit.json').read_text())
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',15)
groups={
 'sword-attacks':[r for r in rows if r['source'].startswith('Sword and Shield Pack/') and any(t in r['source'] for t in ['attack','slash','kick'])],
 'sword-idles-blocks':[r for r in rows if r['source'].startswith('Sword and Shield Pack/') and any(t in r['source'] for t in ['idle','block'])],
 'hammer-attacks':[r for r in rows if r['source'].startswith('Great Sword Pack/') and any(t in r['source'] for t in ['attack','slash'])],
 'hammer-idles':[r for r in rows if r['source'].startswith('Great Sword Pack/') and 'idle' in r['source']],
 'assassin':[r for r in rows if '/' not in r['source']],
 'neutral-idles-deaths':[r for r in rows if r['source'].startswith('Pro Magic Pack/') and any(t in r['source'].lower() for t in ['standing idle','death','small from front']) and 'crouch' not in r['source'].lower()]
}
for group,items in groups.items():
 for page in range(0,len(items),6):
    subset=items[page:page+6]
    im=Image.new('RGB',(1560,70+len(subset)*245),(20,27,39)); d=ImageDraw.Draw(im)
    d.text((20,15),group+' / cyan=left, orange=right / hip XY centered / 6 evenly spaced samples',font=font,fill='white')
    for row,r in enumerate(subset):
        y=70+row*245
        d.line((15,y-7,1540,y-7),fill=(70,80,95))
        d.text((20,y),Path(r['source']).stem+f"  | {r['duration_seconds']:.2f}s | hip delta Y={r['hips_delta_blender_xyz'][1]:+.2f}",font=font,fill='white')
        allpts=[p for s in r['samples'] for ends in s['bones'].values() for p in ends]
        height=max(p[2] for p in allpts)-min(p[2] for p in allpts)
        scale=158/max(height,.1)
        zmin=min(p[2] for p in allpts)
        for col,s in enumerate(r['samples']):
            cx=125+col*258; base=y+213; hip=s['bones']['Hips'][0]
            def project(p):
                x,depth,z=p[0]-hip[0],p[1]-hip[1],p[2]-zmin
                return (cx+scale*(.86*x+.5*depth),base-scale*(z-.16*depth))
            for name,(a,b) in s['bones'].items():
                color=(78,210,230) if name.startswith('Left') else (250,157,87) if name.startswith('Right') else (219,223,230)
                pa,pb=project(a),project(b)
                d.line([pa,pb],fill=color,width=5)
                d.ellipse((pa[0]-3,pa[1]-3,pa[0]+3,pa[1]+3),fill=color)
            d.text((cx-20,y+223),f"{s['time']:.2f}s",font=small,fill=(160,170,185))
    im.save(ROOT/f'{group}-{page//6+1}.png')
print('Contact sheets saved')
