"""Archive the generated shot for production QA and compare against its 3D source."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, re, subprocess, urllib.request

out = Path(__file__).resolve().parent
root = out.parents[4]
ffmpeg = root/'Logs/EndingVideoRuntime/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
font = root/'Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'
result = json.loads((out/'result.json').read_text(encoding='utf-8'))
url = result['result_url']
generated = out/'R01-Seedance25-v1.mp4'
if not generated.exists():
    with urllib.request.urlopen(url, timeout=120) as response:
        generated.write_bytes(response.read())

def ff(args):
    subprocess.run([str(ffmpeg),'-hide_banner','-loglevel','error','-y',*args],check=True,cwd=root)

qa = out/'qa'
qa.mkdir(exist_ok=True)
times = [0, 1, 2, 3, 4.45]
for stem in ['R01-motion-reference','R01-Seedance25-v1']:
    for i,t in enumerate(times):
        ff(['-ss',str(t),'-i',str(out/(stem+'.mp4')),'-frames:v','1','-q:v','2',str(qa/f'{stem}-{i}.jpg')])

sheet = Image.new('RGB',(1600,5*492+68),(14,19,28))
d = ImageDraw.Draw(sheet)
typeface = ImageFont.truetype(str(font),24)
d.text((24,20),'R01 · 3D 프리비즈',font=typeface,fill='white')
d.text((824,20),'Seedance 2.5 · 첫 생성',font=typeface,fill='white')
for i,t in enumerate(times):
    y=68+i*492
    for x,stem in [(0,'R01-motion-reference'),(800,'R01-Seedance25-v1')]:
        im=Image.open(qa/f'{stem}-{i}.jpg').convert('RGB').resize((800,450),Image.Resampling.LANCZOS)
        sheet.paste(im,(x,y))
        d.text((x+16,y+455),f'{t:.2f}s',font=typeface,fill=(185,200,220))
sheet.save(out/'R01-frame-comparison.jpg',quality=93)

font_filter='Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'
filter_graph=(
    "[0:v]fps=24,scale=960:540,setsar=1,pad=960:600:0:60:color=0x101620,"
    f"drawtext=fontfile='{font_filter}':text='3D PREVIS / CAMERA + BLOCKING':fontsize=24:fontcolor=white:x=24:y=18[a];"
    "[1:v]fps=24,scale=960:540,setsar=1,pad=960:600:0:60:color=0x101620,"
    f"drawtext=fontfile='{font_filter}':text='SEEDANCE 2.5 / FIRST TEST':fontsize=24:fontcolor=white:x=24:y=18[b];"
    '[a][b]hstack=inputs=2[v]'
)
ff(['-i',str(out/'R01-motion-reference.mp4'),'-i',str(generated),'-filter_complex',filter_graph,
    '-map','[v]','-t','5','-an','-c:v','libx264','-crf','18','-preset','slow','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'R01-Previs-vs-Seedance.mp4')])
probe=subprocess.run([str(ffmpeg),'-hide_banner','-i',str(generated)],capture_output=True,text=True,encoding='utf-8',errors='replace')
(qa/'generated-media-info.txt').write_text(probe.stderr,encoding='utf-8')
ff(['-i',str(generated),'-f','null','-'])
print(json.dumps({'generated':str(generated),'comparison':str(out/'R01-Previs-vs-Seedance.mp4'),'contact_sheet':str(out/'R01-frame-comparison.jpg'),'decode':'passed'},ensure_ascii=False))
