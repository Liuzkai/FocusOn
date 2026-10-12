"""Require an existing immutable version tag pointing at its workflow source."""
import os
import re
import subprocess
import xml.etree.ElementTree as ET
tag = os.environ['RELEASE_TAG']
assert re.fullmatch(r'v\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', tag), 'Expected a version tag'
assert tag != 'v0.1.0', 'v0.1.0 is preserved; use its historical append workflow only'
commit = subprocess.check_output(['git', 'rev-parse', '--verify', f'refs/tags/{tag}^{{commit}}'], text=True).strip()
assert commit == subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(), 'Select the workflow at the same tag being released'
assert ET.parse('Directory.Build.props').findtext('.//Version') == tag[1:], 'Tag and application Version must agree'
print('commit=' + commit)
print('tag=' + tag)
