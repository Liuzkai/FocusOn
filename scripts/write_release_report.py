"""Save a small publication receipt on a status branch for Git-only environments."""
import base64
import json
import os
import pathlib
import subprocess
import urllib.error
from github_api import api

root = pathlib.Path(__file__).resolve().parent.parent
repo = os.environ['GH_REPO']
assert repo == 'Liuzkai/FocusOn'
report = {'sourceCommit': subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(), 'runUrl':os.environ.get('RUN_URL')}
for name, filename in [('release','linux-release-result.json'),('about','repository-about-result.json')]:
    path = root / 'artifacts' / filename
    report[name] = json.loads(path.read_text()) if path.exists() else {'status':'failed', 'error':'Stage did not complete; see workflow run.'}
branch = 'heads/codex/release-status'
try:
    parent = api(f'repos/{repo}/git/ref/{branch}')['object']['sha']
except urllib.error.HTTPError as error:
    if error.code != 404: raise
    parent = None
content = json.dumps(report, indent=2).encode()
blob = api(f'repos/{repo}/git/blobs','POST',{'content':base64.b64encode(content).decode(),'encoding':'base64'})['sha']
tree = api(f'repos/{repo}/git/trees','POST',{'tree':[{'path':'linux-release.json','mode':'100644','type':'blob','sha':blob}]})['sha']
commit = api(f'repos/{repo}/git/commits','POST',{'message':'Record verified Linux release result','tree':tree,'parents':[parent] if parent else []})['sha']
if parent:
    api(f'repos/{repo}/git/refs/{branch}','PATCH',{'sha':commit,'force':False})
else:
    api(f'repos/{repo}/git/refs','POST',{'ref':'refs/' + branch,'sha':commit})
print('Saved publication receipt for ' + report['sourceCommit'])
