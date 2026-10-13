"""Select only the explicitly requested or uniquely changed approval record."""
import json
import os
import pathlib
import re
import subprocess

root = pathlib.Path(__file__).resolve().parent.parent

def select_tag(requested, event, head, changed):
    if requested:
        tag = requested
    else:
        assert event.get('ref') == 'refs/heads/main', 'Publication pushes must target main'
        assert event.get('after') == head, 'Approval selection must use the event commit'
        assert re.fullmatch(r'[0-9a-f]{40}', event.get('before', '')) and event['before'] != '0' * 40
        paths = changed(event['before'], head)
        assert len(paths) == 1, 'Publish exactly one changed approval record per push'
        path = pathlib.PurePosixPath(paths[0])
        assert str(path.parent) == '.github/release-publication' and path.suffix == '.json'
        tag = path.stem
    assert re.fullmatch(r'v\d+\.\d+\.\d+', tag), 'A stable approved version is required'
    return tag

def main():
    requested = os.environ.get('REQUESTED_TAG', '')
    event = json.loads(pathlib.Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    def changed(before, after):
        return subprocess.check_output(['git', 'diff', '--name-only', '--diff-filter=AM', before, after,
            '--', '.github/release-publication/*.json'], cwd=root, text=True).splitlines()
    tag = select_tag(requested, event, head, changed)
    approval = json.loads((root / '.github/release-publication' / (tag + '.json')).read_text())
    assert approval['tag'] == tag and approval['publishExistingDraft'] is True and approval['authorization'].strip()
    print('tag=' + tag)

if __name__ == '__main__':
    main()
