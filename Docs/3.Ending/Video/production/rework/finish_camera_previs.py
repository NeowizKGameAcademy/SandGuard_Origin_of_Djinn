import json, re, subprocess
from pathlib import Path
from PIL import Image, ImageDraw
here=Path(__file__).resolve().parent
root=here.parents[4]
ff=str(root/'Logs/EndingVideoRuntime/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
frames=sorted((here/'frames').glob('f_*.png'))
assert len(frames)==192,len(frames)
for f in frames:
    with Image.open(f) as im:assert im.size==(1280,720)
labels=['사전 촬영 테스트 · 최종 미술/연기 적용 전','전경 기둥 옆으로 이동','인물의 측면으로 시점 전환','등 뒤에서 마석이 드러남']
for i,label in enumerate(labels):(here/f'label-{i}.txt').write_text(label,encoding='utf-8')
font='../../../../../Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'
filters=[]
for i in range(4):
    enable=['1','lt(t,2)','between(t,2,5.5)','gte(t,5.5)'][i]
    filters.append(f"drawtext=fontfile='{font}':textfile=label-{i}.txt:fontsize={24 if i==0 else 27}:fontcolor=white:x=28:y={26 if i==0 else 659}:box=1:boxcolor=black@0.6:boxborderw=10:enable='{enable}'")
out=here/'Camera-Study-8s.mp4'
cmd=[ff,'-hide_banner','-y','-framerate','24','-start_number','1','-i','frames/f_%04d.png','-frames:v','192','-vf',','.join(filters),'-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-movflags','+faststart',str(out)]
r=subprocess.run(cmd,cwd=here,capture_output=True,text=True,encoding='utf-8',errors='replace');(here/'encode.log').write_text(r.stderr,encoding='utf-8');r.check_returncode()
check=subprocess.run([ff,'-hide_banner','-v','error','-xerror','-i',str(out),'-f','null','-'],capture_output=True,text=True);check.check_returncode()
(here/'decode-check.txt').write_text(check.stderr or 'All frames decoded without errors.\n',encoding='utf-8')
contact=Image.new('RGB',(1280,420),(20,20,20));draw=ImageDraw.Draw(contact)
for i,f in enumerate([1,24,48,72,96,120,144,192]):
    with Image.open(here/f'frames/f_{f:04}.png') as im:contact.paste(im.resize((320,180)),((i%4)*320,(i//4)*210))
    draw.text(((i%4)*320+8,(i//4)*210+186),f'{(f-1)/24:.2f}s',fill='white')
contact.save(here/'camera-contact.jpg')
report=dict(output=str(out),durationSeconds=8,fps=24,frames=192,resolution=[1280,720],bytes=out.stat().st_size,decodeOk=True,source='Blender 5.2 / actual 3D geometry and animated camera',higgsfieldCreditsUsed=0,scope='camera blocking only; final acting, grip, art and effects are unapproved')
(here/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
