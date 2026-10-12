"""GitHub workflow API helper. Credentials are read from the runner environment only."""
import json
import os
import urllib.error
import urllib.request

def api(path, method='GET', data=None):
    request = urllib.request.Request('https://api.github.com/' + path, method=method,
        data=None if data is None else json.dumps(data).encode(), headers={
            'Authorization': 'Bearer ' + os.environ['GH_TOKEN'], 'Accept': 'application/vnd.github+json',
            'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'FocusOn-release', 'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=60) as response:
        content = response.read()
        return json.loads(content) if content else None

def error_summary(error):
    if isinstance(error, urllib.error.HTTPError):
        try:
            detail = json.loads(error.read()).get('message', error.reason)
        except (ValueError, UnicodeDecodeError):
            detail = error.reason
        return f'GitHub HTTP {error.code}: {detail}'
    return str(error)
