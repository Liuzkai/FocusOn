"""Retry a committed, exact-source failed release after native jobs already passed."""
import json
import os
import pathlib
import re
import subprocess
from github_api import api

repo = os.environ['GH_REPO']
assert repo == 'Liuzkai/FocusOn'
requested = os.environ.get('REQUEST_FILE')
if not requested:
    before = os.environ['PUSH_BEFORE']
    assert re.fullmatch(r'[0-9a-f]{40}', before)
    changed = subprocess.check_output(['git', 'diff', '--name-only', before, 'HEAD', '--', '.github/release-retries'], text=True).splitlines()
    assert len(changed) == 1, 'Change exactly one committed retry request'
    requested = changed[0]
assert re.fullmatch(r'\.github/release-retries/v\d+\.\d+\.\d+\.json', requested)
request = json.loads(pathlib.Path(requested).read_text())
run_id, tag, source = request['run_id'], request['version_tag'], request['source_commit']
assert re.fullmatch(r'\d+', run_id) and re.fullmatch(r'v\d+\.\d+\.\d+', tag) and re.fullmatch(r'[0-9a-f]{40}', source)
assert pathlib.Path(requested).stem == tag
subprocess.run(['git', 'merge-base', '--is-ancestor', source, 'origin/main'], check=True)
reference = api(f'repos/{repo}/git/ref/tags/{tag}')['object']
while reference['type'] == 'tag':
    reference = api(f'repos/{repo}/git/tags/{reference["sha"]}')['object']
assert reference['type'] == 'commit' and reference['sha'] == source
run = api(f'repos/{repo}/actions/runs/{run_id}')
assert run['head_sha'] == source and run['path'] == '.github/workflows/release.yml' and run['status'] == 'completed'
if run['conclusion'] == 'success':
    print('Release run already passed; no retry needed: ' + run['html_url'])
else:
    assert run['conclusion'] == 'failure'
    jobs = api(f'repos/{repo}/actions/runs/{run_id}/jobs?per_page=100')['jobs']
    native = [job for job in jobs if job['name'].startswith('build / Native ')]
    expected = {'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64'}
    assert {job['name'].removeprefix('build / Native ') for job in native} == expected
    assert all(job['conclusion'] == 'success' for job in native), 'Native validation must already have passed'
    assert {job['name'] for job in jobs if job['conclusion'] == 'failure'} == {'draft'}, 'Retry only a failed draft job'
    assert run['run_attempt'] == 1, 'Review any repeated failure before retrying again'
    api(f'repos/{repo}/actions/runs/{run_id}/rerun-failed-jobs', 'POST')
    print('Requested failed-job retry; successful native jobs and immutable tag retained: ' + run['html_url'])
