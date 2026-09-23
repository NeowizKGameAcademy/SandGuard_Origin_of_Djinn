"""Assemble the seven generated ending scenes into the approved 49-second cut."""
import argparse
import json
import re
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--ffmpeg', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parent
game = root.parents[3]
ffmpeg = str(Path(args.ffmpeg).resolve())
qa = root / 'qa'
qa.mkdir(exist_ok=True)
durations = [6, 6, 6, 9, 8, 8, 6]
names = ['01_Victory', '02_Rupture', '03_Village', '04_Seal_Price',
         '05_Last_Wish', '06_Seal', '07_Lamp']
music = game / 'Docs/3.Ending/3.The Eternal Wait.mp3'

def run(cmd, name):
    with (qa / name).open('w', encoding='utf-8') as log:
        result = subprocess.run(cmd, cwd=root, stdout=log, stderr=log)
    if result.returncode:
        raise RuntimeError(f'{name} failed; see {qa / name}')

metadata = []
for name, duration in zip(names, durations):
    source = root / 'clips' / (name + '.mp4')
    if not source.is_file():
        raise FileNotFoundError(source)
    result = subprocess.run([ffmpeg, '-hide_banner', '-i', str(source)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    match = re.search(r'Duration: (\d+):(\d+):([\d.]+)', result.stderr)
    if not match or 'Audio:' not in result.stderr:
        raise ValueError(f'Missing duration/audio: {source}')
    actual = int(match[1]) * 3600 + int(match[2]) * 60 + float(match[3])
    if actual < duration - .05:
        raise ValueError(f'Clip too short: {source}: {actual} < {duration}')
    metadata.append({'file':source.name, 'source_seconds':actual, 'edit_seconds':duration})

raw = root / 'SandGuard-Ending49-raw.mp4'
command = [ffmpeg, '-hide_banner', '-y']
filters = []
for i, (name, duration) in enumerate(zip(names, durations)):
    command += ['-threads', '2', '-i', str(root / 'clips' / (name + '.mp4'))]
    filters += [
        f'[{i}:v]trim=duration={duration},setpts=PTS-STARTPTS,fps=24,scale=1920:1080,setsar=1,format=yuv420p[v{i}]',
        f'[{i}:a]atrim=duration={duration},asetpts=PTS-STARTPTS,aresample=48000,aformat=channel_layouts=stereo,apad,atrim=duration={duration},afade=t=in:d=0.03,afade=t=out:st={duration-.03}:d=0.03[a{i}]'
    ]
filters.append(''.join(f'[v{i}][a{i}]' for i in range(7)) + 'concat=n=7:v=1:a=1[v][a]')
command += ['-filter_complex_threads', '2', '-filter_complex', ';'.join(filters),
            '-map', '[v]', '-map', '[a]', '-t', '49', '-c:v', 'libx264',
            '-threads', '4', '-preset', 'fast', '-crf', '17', '-c:a', 'aac',
            '-b:a', '256k', '-ar', '48000', '-movflags', '+faststart', str(raw)]
run(command, 'assembly.log')

(root / 'title.txt').write_text('첫 번째 지니', encoding='utf-8')
font = '../../../../Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'
music_gain = 'if(lt(t,2),0.40*t/2,if(lt(t,18),0.40,if(lt(t,27),0.23,if(lt(t,35),0.13,if(lt(t,40),0.13+(t-35)*0.13,if(lt(t,43),0.78,0.40))))))'
filter_graph = (
    "[0:v]fade=t=in:st=0:d=0.25,fade=t=out:st=46.2:d=0.8,"
    f"drawtext=fontfile='{font}':textfile=title.txt:fontcolor=0xE4DCCB:fontsize=58:"
    "x=(w-tw)/2:y=(h-th)/2:alpha='if(lt(t,47),0,if(lt(t,47.35),(t-47)/0.35,if(lt(t,48.55),1,(49-t)/0.45)))',format=yuv420p[v];"
    "[0:a]aresample=48000,loudnorm=I=-20:TP=-3:LRA=12,afade=t=out:st=47.3:d=1.7[native];"
    f"[1:a]atrim=start=0:duration=49,asetpts=PTS-STARTPTS,aresample=48000,volume='{music_gain}':eval=frame,afade=t=out:st=46:d=3[music];"
    '[native][music]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.93:level=false,aresample=48000[a]'
)
output = root / 'SandGuard-Ending49-v1.mp4'
command = [ffmpeg, '-hide_banner', '-y', '-threads', '2', '-i', str(raw),
           '-i', str(music), '-filter_complex_threads', '2', '-filter_complex', filter_graph,
           '-map', '[v]', '-map', '[a]', '-t', '49', '-r', '24', '-c:v', 'libx264',
           '-threads', '4', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p',
           '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-ac', '2',
           '-movflags', '+faststart', '-map_metadata', '-1', str(output)]
run(command, 'finish.log')

result = subprocess.run([ffmpeg, '-hide_banner', '-v', 'error', '-xerror', '-i', str(output),
                         '-f', 'null', '-'], capture_output=True, text=True)
(qa / 'decode-check.txt').write_text(result.stderr or 'Complete video and audio decoded without errors.\n', encoding='utf-8')
result.check_returncode()
run([ffmpeg, '-hide_banner', '-y', '-i', str(output), '-vf',
     'fps=1/4,scale=480:-1,tile=4x4:padding=4:margin=4', '-frames:v', '1',
     '-update', '1', str(qa / 'final-contact-sheet.jpg')], 'contact-sheet.log')
for time, name in [(47.8, 'final-title.png'), (48.9583, 'last-frame.png')]:
    run([ffmpeg, '-hide_banner', '-y', '-ss', str(time), '-i', str(output),
         '-frames:v', '1', '-update', '1', str(qa / name)], name + '.log')
report = {'output':str(output), 'bytes':output.stat().st_size, 'edit_seconds':sum(durations),
          'fps':24, 'expected_video_frames':1176, 'resolution':'1920x1080',
          'music_source':str(music), 'music_source_interval':[0,49],
          'speech':'이 사막에 살아가는 모두에게 자유를 돌려줘.',
          'decode_ok':True, 'sources':metadata}
(qa / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
