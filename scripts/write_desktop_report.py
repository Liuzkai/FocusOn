"""Record build/draft evidence on a separate status branch, with no secrets."""
import base64
import json
import os
import pathlib
import subprocess
import urllib.error
from github_api import api
repo = os.environ['GH_REPO']; assert repo == 'Liuzkai/FocusOn'
run_id = os.environ['RUN_ID']; assert run_id.isdigit()
root = pathlib.Path(__file__).resolve().parent.parent
report = {'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
          'runUrl': f'https://github.com/{repo}/actions/runs/{run_id}', 'validationResult': os.environ['VALIDATION_RESULT'],
          'jobs': [{key: job[key] for key in ['name','status','conclusion','html_url']} for job in api(f'repos/{repo}/actions/runs/{run_id}/jobs?per_page=100')['jobs']], 'evidence': {}}
for name in ['verification.json', 'mac-signing.json']:
    for path in (root / 'artifacts/evidence').rglob(name):
        value = json.loads(path.read_text()); report['evidence'].setdefault(path.parent.name.removeprefix('evidence-'), {})[name] = value
result_file = root / 'artifacts/desktop-release-result.json'
if result_file.exists(): report['release'] = json.loads(result_file.read_text())
branch = 'heads/codex/desktop-status'
try: parent = api(f'repos/{repo}/git/ref/{branch}')['object']['sha']
except urllib.error.HTTPError as error:
    if error.code != 404: raise
    parent = None
blob = api(f'repos/{repo}/git/blobs', 'POST', {'content': base64.b64encode((json.dumps(report, indent=2) + '\n').encode()).decode(), 'encoding':'base64'})['sha']
tree = api(f'repos/{repo}/git/trees', 'POST', {'tree':[{'path':'desktop-build.json','mode':'100644','type':'blob','sha':blob}]})['sha']
commit = api(f'repos/{repo}/git/commits', 'POST', {'message':'Record desktop validation and draft status','tree':tree,'parents':[parent] if parent else []})['sha']
if parent: api(f'repos/{repo}/git/refs/{branch}', 'PATCH', {'sha':commit,'force':False})
else: api(f'repos/{repo}/git/refs', 'POST', {'ref':'refs/' + branch, 'sha':commit})
print('Saved desktop receipt: ' + report['runUrl'])
