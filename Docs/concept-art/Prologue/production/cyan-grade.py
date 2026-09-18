import colorsys
N=33
def smooth(a,b,x):
 t=max(0.,min(1.,(x-a)/(b-a))); return t*t*(3-2*t)
with open('cyan.cube','w') as f:
 f.write('TITLE "SandGuard cyan magic"\nLUT_3D_SIZE 33\nDOMAIN_MIN 0 0 0\nDOMAIN_MAX 1 1 1\n')
 for bi in range(N):
  for gi in range(N):
   for ri in range(N):
    r,g,b=ri/(N-1),gi/(N-1),bi/(N-1)
    h,s,v=colorsys.rgb_to_hsv(r,g,b)
    w=smooth(.55,.62,h)*(1-smooth(.80,.87,h))*smooth(.08,.3,s)
    target=colorsys.hsv_to_rgb(.535,s,v)
    o=[c*(1-w)+d*w for c,d in zip((r,g,b),target)]
    f.write('%.7f %.7f %.7f\n'%tuple(o))
