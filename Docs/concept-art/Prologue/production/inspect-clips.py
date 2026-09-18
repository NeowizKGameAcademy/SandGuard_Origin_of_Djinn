import argparse, json, subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--ffmpeg', required=True)
p.add_argument('scenes', nargs='+', type=int)
a = p.parse_args()
root = Path(__file__).resolve().parent
for n in a.scenes:
    src = root / 'clips' / f'{n:02d}-v1.mp4'
    dst = root / 'qa' / f'{n:02d}-contact-sheet.png'
    result = subprocess.run([a.ffmpeg, '-hide_banner', '-y', '-i', str(src), '-vf',
        'fps=1,scale=640:-1,tile=4x3:padding=4:margin=4', '-frames:v', '1', str(dst)],
        text=True, capture_output=True)
    (root / 'qa' / f'{n:02d}-probe.txt').write_text(result.stderr, encoding='utf-8')
    result.check_returncode()
    print(json.dumps({'scene': n, 'sheet': str(dst)}))
