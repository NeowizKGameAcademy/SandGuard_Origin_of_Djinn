"""PUT a local production input to its already allocated Higgsfield upload slot."""
from pathlib import Path
import sys
import urllib.request

path=Path(sys.argv[1])
req=urllib.request.Request(sys.argv[2],data=path.read_bytes(),method='PUT',headers={'Content-Type':'video/mp4' if path.suffix=='.mp4' else 'image/jpeg'})
with urllib.request.urlopen(req,timeout=90) as response:
    print(response.status)
