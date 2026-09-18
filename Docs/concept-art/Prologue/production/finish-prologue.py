"""Assemble and finish the seven Higgsfield shots locally with the game's score."""
import argparse, colorsys, json, subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--ffmpeg', required=True)
p.add_argument('--raw', default='SandGuard-Prologue-raw.mp4')
a = p.parse_args()
root = Path(__file__).resolve().parent
game = root.parents[3]
music = game / 'Assets/8.Audio/AudioResource/메인화면 배경음.mp3'

raw = root / a.raw
if not raw.exists():
    durations = [6,5,6,7,7,8,11]
    files = ['01-corrected.mp4','02-v1.mp4','03-v1.mp4','04-v1.mp4',
             '05-lamp-awakening-v1.mp4','06-v1.mp4','07-v1.mp4']
    cmd = [a.ffmpeg,'-hide_banner','-y']
    parts = []
    for i,(file,duration) in enumerate(zip(files,durations)):
        cmd += ['-threads','2','-i',str(root/'clips'/file)]
        parts += [f'[{i}:v]trim=duration={duration},setpts=PTS-STARTPTS,fps=24,scale=1920:1080,setsar=1,format=yuv420p[v{i}]',
                  f'[{i}:a]atrim=duration={duration},asetpts=PTS-STARTPTS,aresample=48000,aformat=channel_layouts=stereo,afade=t=in:d=0.04,afade=t=out:st={duration-.04}:d=0.04[a{i}]']
    parts += [''.join(f'[v{i}][a{i}]' for i in range(7))+'concat=n=7:v=1:a=1[v][a]']
    cmd += ['-filter_complex',';'.join(parts),'-map','[v]','-map','[a]','-t','50',
            '-c:v','libx264','-threads','4','-preset','fast','-crf','16',
            '-c:a','aac','-b:a','256k','-ar','48000','-movflags','+faststart',str(raw)]
    with (root/'qa/local-assembly.log').open('w',encoding='utf-8') as log:
        subprocess.run(cmd,cwd=root,stdout=log,stderr=log,check=True)

def smooth(lo, hi, x):
    t = max(0.0, min(1.0, (x-lo)/(hi-lo)))
    return t*t*(3-2*t)

for mode in ('cyan','unlit'):
    with (root / f'{mode}.cube').open('w', encoding='ascii') as f:
        f.write(f'TITLE "SandGuard {mode}"\nLUT_3D_SIZE 33\nDOMAIN_MIN 0 0 0\nDOMAIN_MAX 1 1 1\n')
        for bi in range(33):
            for gi in range(33):
                for ri in range(33):
                    rgb = (ri/32,gi/32,bi/32)
                    h,s,v = colorsys.rgb_to_hsv(*rgb)
                    w = smooth(.55,.62,h)*(1-smooth(.80,.87,h))*smooth(.08,.3,s)
                    target = colorsys.hsv_to_rgb(.535,s,v) if mode=='cyan' else colorsys.hsv_to_rgb(.095,min(s,.40),v*.48)
                    out = tuple(c*(1-w)+d*w for c,d in zip(rgb,target))
                    f.write('%.7f %.7f %.7f\n'%out)

music_gain = 'if(lt(t,2),0.22*t/2,if(lt(t,15),0.22,if(lt(t,18),0.22-(t-15)*0.045,if(lt(t,30),0.085,if(lt(t,39),0.085+(t-30)*0.065/9,if(lt(t,45),0.15+(t-39)*0.19/6,if(lt(t,48),0.34,0.34*(50-t)/2)))))))'
filters = (
    '[0:v]split=2[base][grade];'
    "[grade]lut3d=unlit.cube:enable='between(t,11,17)'[unlit];"
    "[base][unlit]blend=all_expr='if(gte(Y,H*0.40),B,A)':enable='between(t,11,17)'[pre];"
    "[pre]lut3d=cyan.cube:enable='gte(t,24)',fade=t=out:st=49.0:d=0.8,format=yuv420p[v];"
    '[0:a]atrim=duration=50,asetpts=PTS-STARTPTS,aresample=48000,loudnorm=I=-21:TP=-3:LRA=12,afade=t=in:d=0.08,afade=t=out:st=48.8:d=1.2[sfx];'
    f"[1:a]atrim=duration=50,asetpts=PTS-STARTPTS,aresample=48000,volume='{music_gain}':eval=frame[music];"
    '[sfx][music]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.94:level=false,aresample=48000[a]'
)
output = root / 'SandGuard-Prologue-v1.mp4'
cmd = [a.ffmpeg,'-hide_banner','-y','-i',str(root/a.raw),'-i',str(music),
       '-filter_complex',filters,'-map','[v]','-map','[a]','-t','50','-r','24',
       '-c:v','libx264','-preset','medium','-crf','18','-pix_fmt','yuv420p',
       '-c:a','aac','-ar','48000','-ac','2','-b:a','192k','-movflags','+faststart',
       '-map_metadata','-1',str(output)]
with (root/'qa/finish-render.log').open('w',encoding='utf-8') as log:
    subprocess.run(cmd,cwd=root,stdout=log,stderr=log,check=True)

verification = subprocess.run([a.ffmpeg,'-hide_banner','-i',str(output),'-af','volumedetect',
    '-vf','blackdetect=d=0.10:pix_th=0.02','-f','null','-'],capture_output=True,text=True)
(root/'qa/final-decode-check.txt').write_text(verification.stderr,encoding='utf-8')
verification.check_returncode()
subprocess.run([a.ffmpeg,'-hide_banner','-y','-i',str(output),'-vf',
    'fps=1/5,scale=640:-1,tile=5x2:padding=4:margin=4','-frames:v','1',str(root/'qa/final-contact-sheet.png')],
    capture_output=True,check=True)
subprocess.run([a.ffmpeg,'-hide_banner','-y','-ss','49.9583','-i',str(output),'-frames:v','1',
    str(root/'qa/final-last-frame.png')],capture_output=True,check=True)
print(json.dumps({'output':str(output),'bytes':output.stat().st_size,'duration_seconds':50,
                  'music_source':str(music),'decoded_without_errors':True},ensure_ascii=False))
