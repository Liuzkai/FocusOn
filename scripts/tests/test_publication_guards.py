import unittest
from unittest.mock import patch
import contextlib, copy, hashlib, io, json, os, pathlib, runpy, shutil, subprocess, sys, tarfile, tempfile, types, urllib.error, zipfile
root = pathlib.Path(__file__).resolve().parents[2]
source = 'c0938e72afc53a063fc26e0ecc399e8d37b4e398'

class PublicationGuardTests(unittest.TestCase):
 def check(self, scenario):
  with tempfile.TemporaryDirectory(prefix='publication-guard-',) as temporary:
   tag = "v0.1.7" if "installer" in scenario else "v0.1.5"
   fixture=pathlib.Path(temporary);(fixture/'scripts').mkdir();(fixture/'.github/release-publication').mkdir(parents=True);archives=fixture/'downloads';archives.mkdir()
   shutil.copyfile(root/'scripts/publish_approved_release.py',fixture/'scripts/publish_approved_release.py')
   assets=[]
   for rid in ['win-x64','win-arm64','linux-x64','linux-arm64']:
    name='FocusOn-'+rid+('.zip' if rid.startswith('win') else '.tar.gz'); path=archives/name
    manifest=json.dumps({'sourceCommit':'0'*40 if scenario=='wrong_manifest_source' else source,'runtime':rid,'buildKind':'release'}).encode()
    member='FocusOn-'+rid+'/manifest.json'
    if rid.startswith('win'):
     with zipfile.ZipFile(path,'w') as stream:stream.writestr(member,manifest)
    else:
     with tarfile.open(path,'w:gz') as stream:
      item=tarfile.TarInfo(member);item.size=len(manifest);stream.addfile(item,io.BytesIO(manifest))
    (archives/(name+'.sha256')).write_text(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+name+'\n')
   if "installer" in scenario and scenario != "missing_installer":
    for rid in ['win-x64','win-arm64']:
     path=archives/('FocusOn-'+rid+'-Setup.exe');path.write_bytes(b'MZ synthetic verified installer')
     (archives/(path.name+'.sha256')).write_text(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.name+'\n')
   for i,path in enumerate(sorted(archives.iterdir())):
    assets.append({'id':i+1,'name':path.name,'size':path.stat().st_size,'digest':'sha256:'+hashlib.sha256(path.read_bytes()).hexdigest(),'browser_download_url':'https://github.com/fixture/'+path.name})
   request={'tag':tag,'publishExistingDraft':True,'authorization':'User explicitly approved public release','sourceCommit':source,'buildRunId':38342256279,'assets':copy.deepcopy(assets)}
   (fixture/('.github/release-publication/'+tag+'.json')).write_text(json.dumps(request))
   release={'id':11,'tag_name':tag,'target_commitish':source,'draft':scenario!='already_public','prerelease':False,'published_at':'2026-10-12T00:00:00Z' if scenario=='already_public' else None,'html_url':'https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.5','body':'1h / Today, cross-midnight review, label drag. This release stays a draft.','assets':copy.deepcopy(assets)}
   if scenario in ['aliased_draft','aliased_public','unexpected_release_tag','wrong_alias_source']:
    request['releaseId']=11
    (fixture/('.github/release-publication/'+tag+'.json')).write_text(json.dumps(request))
    release['tag_name']='v0.1.99' if scenario=='unexpected_release_tag' else 'untagged-123abc'
    if scenario=='aliased_public':release['draft']=False
    if scenario=='wrong_alias_source':release['target_commitish']='0'*40
   if scenario=='changed_asset':release['assets'][0]['digest']='sha256:'+'0'*64
   if scenario=='unexpected_mac_asset':release['assets'].append({'id':20,'name':'FocusOn-osx-x64.zip','size':1,'digest':'sha256:'+'0'*64})
   historical={'id':10,'tag_name':'v0.1.0','assets':[{'id':100,'name':'original.zip','size':5,'digest':'sha256:original'}]}
   old_draft={'id':12,'tag_name':'v0.1.4','target_commitish':'eafc2053e5377dfe52af00013e4d5975c75d2893','name':'FocusOn v0.1.4','body':'Original older draft notes','draft':True,'prerelease':False,'assets':[]}
   if scenario=='untagged_public':old_draft['draft']=False
   downloaded=False
   jobs=[{'name':'build / Native '+rid,'status':'completed','conclusion':'success'} for rid in ['win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64']]
   if scenario=='missing_native_job':jobs.pop()
   patches=[]
   def api(path,method='GET',data=None):
    if '/git/ref/tags/' in path:
     if path.endswith('/v0.1.4'):
      missing = scenario in ['untagged_draft','untagged_public'] or (scenario=='draft_tag_appeared' and not downloaded) or (scenario=='older_tag_deleted' and downloaded)
      if missing or scenario=='draft_ref_forbidden':
       raise urllib.error.HTTPError('https://api.github.com/'+path,403 if scenario=='draft_ref_forbidden' else 404,'Synthetic ref error',{},None)
     name=path.rsplit('/',1)[-1];return {'object':{'type':'commit','sha':('0'*40 if scenario=='changed_tag' else source) if name==tag else ('0'*40 if scenario=='older_tag_changed' and downloaded and name=='v0.1.4' else '43d6dfd901d708ce27d13fcfb691e3061f66c405')}}
    if '/actions/runs/' in path:
     if path.endswith('/jobs?per_page=100'):return {'jobs':copy.deepcopy(jobs)}
     return {'head_sha':source,'status':'completed','conclusion':'failure' if scenario=='failed_build' else 'success'}
    if '/releases?' in path:return copy.deepcopy([release,historical,old_draft])
    if path.endswith('/releases/12'):
     value=copy.deepcopy(old_draft)
     if scenario=='older_body_changed' and downloaded:value['body']='Concurrent change'
     return value
    if path.endswith('/releases/10'):return copy.deepcopy(historical)
    if path.endswith('/releases/11'):
     if method=='PATCH':patches.append(copy.deepcopy(data));release.update(data);release['published_at']='2026-10-12T00:00:00Z'
     return copy.deepcopy(release)
    raise AssertionError('Unexpected API: '+path)
   module=types.ModuleType('github_api');module.api=api;module.error_summary=str;sys.modules['github_api']=module
   def download(arguments,check,stdout=None):
    nonlocal downloaded
    downloaded=True
    if arguments[:2]==['gh','api']:
     asset_id=int(arguments[2].rsplit('/',1)[-1]);asset=next(a for a in assets if a['id']==asset_id)
     assert arguments[3:]==['--header','Accept: application/octet-stream']
     stdout.write((archives/asset['name']).read_bytes())
     return
    assert arguments[:3]==['gh','release','download']
    destination=pathlib.Path(arguments[-1])
    for path in archives.iterdir():shutil.copyfile(path,destination/path.name)
    if scenario=='corrupt_download':(destination/'FocusOn-win-x64.zip').write_bytes(b'changed')
    if scenario=='corrupt_installer':(destination/'FocusOn-win-arm64-Setup.exe').write_bytes(b'changed')
   real_run=subprocess.run;subprocess.run=download;env=patch.dict(os.environ,{'GH_REPO':'Liuzkai/FocusOn','RELEASE_TAG':tag});env.start()
   succeeded=False
   try:
    with contextlib.redirect_stdout(io.StringIO()):runpy.run_path(str(fixture/'scripts/publish_approved_release.py'),run_name='__main__')
    succeeded=True
   except (AssertionError,urllib.error.HTTPError):pass
   finally:subprocess.run=real_run;env.stop();sys.modules.pop('github_api',None)
   assert succeeded == (scenario in ['valid','already_public','valid_installer','untagged_draft','aliased_draft']),scenario
   assert bool(patches)==(scenario in ['valid','valid_installer','untagged_draft','aliased_draft']),scenario
   report=json.loads((fixture/'artifacts/desktop-release-result.json').read_text())
   assert report['status']==('published' if succeeded else 'failed'),scenario
   if succeeded:
    assert len(report['preservedReleases'])==2
    if scenario=='untagged_draft':assert report['preservedReleases'][1]['tagObject'] is None
    if scenario=='aliased_draft':
     assert release['tag_name']==tag and report['releaseId']==11
   if scenario=='valid':
    assert '1h / Today, cross-midnight review, label drag.' in release['body']
    assert 'This release stays a draft.' not in release['body']

for scenario in ['valid_installer', 'missing_installer', 'corrupt_installer', 'valid', 'changed_tag', 'failed_build', 'missing_native_job', 'changed_asset', 'wrong_manifest_source', 'corrupt_download', 'unexpected_mac_asset', 'already_public', 'older_body_changed', 'older_tag_changed', 'untagged_draft', 'untagged_public', 'draft_tag_appeared', 'older_tag_deleted', 'draft_ref_forbidden', 'aliased_draft', 'aliased_public', 'unexpected_release_tag', 'wrong_alias_source']:
 setattr(PublicationGuardTests, 'test_' + scenario, lambda self, value=scenario: self.check(value))
