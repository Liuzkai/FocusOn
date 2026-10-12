"""Apply the reviewed English About metadata when repository permissions allow it."""
import json
import os
import pathlib
from github_api import api, error_summary

root = pathlib.Path(__file__).resolve().parent.parent
repo = os.environ['GH_REPO']
assert repo == 'Liuzkai/FocusOn'
wanted = json.loads((root / '.github/about.json').read_text())
result = {'status': 'blocked', 'desired': wanted}
try:
    current = api(f'repos/{repo}')
    homepage = current.get('homepage') or wanted['homepage']
    if current.get('description') != wanted['description'] or current.get('homepage') != homepage:
        api(f'repos/{repo}', 'PATCH', {'description': wanted['description'], 'homepage': homepage})
    topics = api(f'repos/{repo}/topics').get('names', [])
    combined = sorted(set(topics) | set(wanted['topics']))
    if len(combined) > 20:
        raise RuntimeError('Too many existing topics; existing topics were preserved.')
    if sorted(topics) != combined:
        api(f'repos/{repo}/topics', 'PUT', {'names': combined})
    actual = api(f'repos/{repo}')
    actual_topics = api(f'repos/{repo}/topics').get('names', [])
    assert actual.get('description') == wanted['description'] and actual.get('homepage') == homepage
    assert set(wanted['topics']) <= set(actual_topics)
    result.update(status='updated', actual={'description':actual['description'], 'homepage':actual['homepage'], 'topics':actual_topics})
    print('Verified English repository About metadata')
except Exception as error:
    result['error'] = error_summary(error)
    print('Repository About requires additional permission: ' + result['error'])
finally:
    path = root / 'artifacts/repository-about-result.json'; path.parent.mkdir(exist_ok=True)
    path.write_text(json.dumps(result, indent=2) + '\n')
