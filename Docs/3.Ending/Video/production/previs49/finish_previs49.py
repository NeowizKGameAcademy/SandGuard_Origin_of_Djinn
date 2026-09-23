"""Mix temporary sound, assemble the exact 49-second review film, and validate it."""
from pathlib import Path
import json, subprocess, sys, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
FF=ROOT/'Logs/EndingVideoRuntime/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
FONT=ROOT/'Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'
SR=48000;DURATION=49;N=SR*DURATION
RNG=np.random.default_rng(20260923)
def run(args,log=None):
    r=subprocess.run([str(FF),'-hide_banner','-y',*map(str,args)],cwd=HERE,capture_output=True,text=True,encoding='utf-8',errors='replace')
    if log:(HERE/log).write_text(r.stderr,encoding='utf-8')
    if r.returncode:raise RuntimeError(r.stderr[-4000:])
    return r
def decode(file,start,duration,filters=None):
    args=[str(FF),'-v','error','-ss',str(start),'-i',str(file),'-t',str(duration),'-vn','-ar',str(SR),'-ac','2']
    if filters:args+=['-af',filters]
    r=subprocess.run(args+['-f','f32le','-'],capture_output=True);r.check_returncode()
    return np.frombuffer(r.stdout,dtype='<f4').reshape(-1,2).copy()
def wavefile(path,x):
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())
def noise(length,low,high):
    n=round(length*SR);f=np.fft.rfftfreq(n,1/SR)
    z=np.fft.rfft(RNG.standard_normal(n));shape=(1-np.exp(-(f/max(1,low))**4))*np.exp(-(f/high)**4)
    v=np.fft.irfft(z*shape,n=n);return v/(np.std(v)+1e-8)
def envelope(n,attack=.03,release=.12):
    e=np.ones(n);a=min(n,round(attack*SR));r=min(n,round(release*SR))
    e[:a]*=np.linspace(0,1,a)**2;e[-r:]*=np.linspace(1,0,r)**2;return e
def prepare_audio():
    t=np.arange(N)/SR;fx=np.zeros((N,2));cues=[]
    def add(start,x,gain=1,pan=0,label=''):
        i=round(start*SR);x=np.asarray(x);length=min(len(x),N-i)
        if length<=0:return
        if x.ndim==1:x=x[:,None]*np.array([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)])[None,:]
        fx[i:i+length]+=x[:length]*gain
        if label:cues.append(dict(time=start,duration=length/SR,cue=label))
    # A quiet exterior under the movement; broad-band gusts are separate from music.
    for channel in [0,1]:fx[:,channel]+=noise(DURATION,50,1800)*.009*(.65+.35*np.sin(t*.63+channel))
    for at in [.45,1.08,1.69,19.38,19.98,20.58,21.15]:
        n=round(.24*SR);x=noise(.24,55,1100)*np.exp(-np.arange(n)/SR*22)
        add(at,x,.05,pan=-.15 if int(at*10)%2 else .15,label='모래 위 발걸음')
    n=round(.85*SR);tt=np.arange(n)/SR
    crack=(noise(.85,250,9000)*np.exp(-tt*12)+.5*np.sin(2*np.pi*(110*tt+90*tt**2))*np.exp(-tt*5))*envelope(n,.003,.12)
    add(4.72,crack,.15,-.15,'마석 균열')
    n=round(5.3*SR);tt=np.arange(n)/SR
    gust=(noise(5.3,35,1600)*.6+np.sin(2*np.pi*47*tt)*.2)*envelope(n,.6,1.4)
    add(6.0,gust,.12,0,'신전에서 마을로 퍼지는 폭풍')
    n=round(2.1*SR);tt=np.arange(n)/SR
    surge=noise(2.1,40,1900)*envelope(n,.35,.6)
    add(16.65,surge,.075,.25,'손을 거두자 되살아나는 폭풍')
    for at,level in [(13.35,.07),(28.55,.095),(45.76,.055)]:
        n=round(1.1*SR);tt=np.arange(n)/SR
        x=sum(np.sin(2*np.pi*f*tt)*np.exp(-tt*(2.4+j)) /(1+j) for j,f in enumerate([660,990,1320]))*envelope(n,.016,.2)
        add(at,x,level,0,'램프 문양의 푸른 공명')
    n=round(8.0*SR);tt=np.arange(n)/SR
    rising=(noise(8.0,45,2300)*.65+.2*np.sin(2*np.pi*(65*tt+6*tt**2)))*envelope(n,2.0,.45)
    add(30.65,rising,.12,0,'마력이 램프로 되돌아오는 흐름')
    n=round(1.2*SR);tt=np.arange(n)/SR
    impact=sum(np.sin(2*np.pi*f*tt)*np.exp(-tt*(7+j*1.5)) /(1+j) for j,f in enumerate([220,587,913,1421]))
    impact+=noise(1.2,50,2500)*np.exp(-tt*27)*.55
    add(39.31,impact*envelope(n,.002,.15),.18,.05,'램프가 바닥에 닿는 충격과 금속 잔향')
    add(39.57,noise(.25,140,1200)*envelope(round(.25*SR),.003,.22),.018,.1,'램프가 기울다 멈추는 마찰')
    for at in [43.05,43.58,44.18]:
        add(at,noise(.18,80,900)*envelope(round(.18*SR),.005,.17),.024,-.2,'아이의 발걸음')
    # Reuse the exact existing Korean take only as timing reference.
    voice=decode(HERE.parent/'clips/05_Last_Wish.mp4',2.0,4.8,'highpass=f=90,lowpass=f=7000')
    voice*=.68/max(.01,float(np.max(np.abs(voice))));voice*=envelope(len(voice),.04,.15)[:,None]
    dialogue=np.zeros((N,2));i=round(23.2*SR);dialogue[i:i+len(voice)]=voice
    music=decode(ROOT/'Docs/3.Ending/3.The Eternal Wait.mp3',0,49)
    music=np.pad(music,((0,max(0,N-len(music))),(0,0)))[:N]
    music*=.19/max(.01,float(np.max(np.abs(music))))
    duck=1-.70*np.clip((t-22.65)/.5,0,1)*np.clip((28.4-t)/.6,0,1)
    music*=duck[:,None];music*=envelope(N,.7,2.4)[:,None]
    fx*=envelope(N,.25,1.8)[:,None]
    mix=music+fx+dialogue;peak=float(np.max(np.abs(mix)))
    if peak>.94:mix*=.94/peak
    wavefile(HERE/'temp-mix.wav',mix)
    wavefile(HERE/'temp-effects.wav',fx)
    wavefile(HERE/'temp-dialogue.wav',dialogue)
    report=dict(durationSeconds=49,sampleRate=SR,channels=2,peakDbFS=float(20*np.log10(np.max(np.abs(mix)))),dialogue=dict(source='../clips/05_Last_Wish.mp4',sourceIn=2.0,sourceOut=6.8,timelineIn=23.2,text='이 사막에 살아가는 모두에게 자유를 돌려줘.',use='임시 대사. 원본의 배경음 일부 포함. 입 모양 애니메이션 미적용.'),music=dict(source='Docs/3.Ending/3.The Eternal Wait.mp3',sourceIn=0,sourceOut=49,use='기존 곡을 활용한 임시 음악 편집'),effects=cues)
    (HERE/'audio-cues.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Prepared 49-second temporary audio mix.',flush=True)
def finish():
    if not (HERE/'temp-mix.wav').exists():prepare_audio()
    frames=sorted((HERE/'frames').glob('f_*.png'))
    assert len(frames)==1176,f'Expected 1176 frames; found {len(frames)}'
    for i,f in enumerate(frames,1):
        assert f.name==f'f_{i:04d}.png'
        with Image.open(f) as im:assert im.size==(1280,720)
    shots=json.loads((HERE/'shot-plan.json').read_text(encoding='utf-8'))
    labels=HERE/'labels';labels.mkdir(exist_ok=True)
    (labels/'previs.txt').write_text('3D 사전촬영',encoding='utf-8')
    (labels/'title.txt').write_text('첫 번째 지니',encoding='utf-8')
    font='../../../../../Assets/9.Font/public/static/alternative/Pretendard-Light.ttf'
    filters=[f"drawtext=fontfile='{font}':textfile=labels/previs.txt:fontsize=21:fontcolor=white@0.82:x=28:y=25:box=1:boxcolor=black@0.4:boxborderw=8:enable='lt(t,47)'"]
    for shot in shots[:-1]:
        file=f"labels/{shot['id']}.txt";(HERE/file).write_text(f"{shot['id']} · {shot['label']}",encoding='utf-8')
        filters.append(f"drawtext=fontfile='{font}':textfile='{file}':fontsize=22:fontcolor=white:x=28:y=h-52:box=1:boxcolor=black@0.5:boxborderw=8:enable='gte(t,{shot['start']})*lt(t,{shot['end']})'")
    filters += ["drawbox=x=0:y=0:w=iw:h=ih:color=black:t=fill:enable='gte(t,47)'",f"drawtext=fontfile='{font}':textfile=labels/title.txt:fontsize=49:fontcolor=white:x=(w-tw)/2:y=(h-th)/2:alpha='min(1,(t-47)/0.35)':enable='gte(t,47)'"]
    out=HERE/'SandGuard-Ending49-Previs.mp4'
    run(['-framerate','24','-start_number','1','-i','frames/f_%04d.png','-i','temp-mix.wav','-vf',','.join(filters),'-t','49','-frames:v','1176','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-movflags','+faststart',out],'encode.log')
    decode_result=run(['-v','error','-xerror','-i',out,'-f','null','-'],'decode-check.txt')
    if not decode_result.stderr:(HERE/'decode-check.txt').write_text('All video and audio frames decoded without errors.\n',encoding='utf-8')
    font_obj=ImageFont.truetype(str(FONT),19)
    sheet=Image.new('RGB',(1280,4*206),(17,20,27));draw=ImageDraw.Draw(sheet)
    for i,shot in enumerate(shots):
        mid=(shot['startFrame']+shot['endFrame'])//2
        if shot['id']=='R16':
            thumb=HERE/'qa/title.png';run(['-ss','48','-i',out,'-frames:v','1',thumb])
        else:thumb=HERE/f'frames/f_{mid:04d}.png'
        with Image.open(thumb) as im:sheet.paste(im.convert('RGB').resize((320,180)),((i%4)*320,(i//4)*206))
        draw.text(((i%4)*320+8,(i//4)*206+183),f"{shot['id']}  {shot['start']:04.1f}–{shot['end']:04.1f}s",font=font_obj,fill='white')
    sheet.save(HERE/'Shot-Contact.jpg',quality=91)
    report=dict(output=str(out),durationSeconds=49,fps=24,frames=1176,resolution=[1280,720],bytes=out.stat().st_size,decodeOk=True,source='Blender 5.2; native 3D cameras, rig animation and proxy effects',higgsfieldCreditsUsed=0,scope='49초 전체 사전촬영. 최종 미술·손가락 연기·표정·입 모양·효과 적용 전.',audio='임시 대사·기존 곡·임시 합성 효과음 포함',shots=len(shots))
    (HERE/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False),flush=True)
if __name__=='__main__':
    if '--prepare-audio' in sys.argv:prepare_audio()
    else:finish()
