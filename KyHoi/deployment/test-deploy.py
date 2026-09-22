"""Exercise stop/backup/rollback ordering with fake external Docker/Caddy commands."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

FAKE = r'''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
name=Path(sys.argv[0]).name; args=sys.argv[1:]
p=Path(os.environ['FAKE_STATE']); state=json.loads(p.read_text())
state['events'].append([name]+args)
def done(code=0, output=''):
 p.write_text(json.dumps(state)); print(output); sys.exit(code)
if name=='sudo':
 if args[0]=='-n': args=args[1:]
 if args[0]=='--': args=args[1:]
 p.write_text(json.dumps(state)); os.execvp(args[0],args)
if name in ('sleep','chown','systemctl'): done()
if name=='caddy': done(1 if os.environ['FAIL_AT']=='validate' else 0)
if name=='curl': done(1 if os.environ['FAIL_AT']=='https' else 0, '{"mode":"rooms"}')
if name=='docker':
 c=state['containers']
 if args[0] in ('info','image','volume','logs'): done()
 if args[:2]==['container','inspect']: done(0 if args[2] in c else 1)
 if args[0]=='inspect':
  if 'Health.Status' in args[2]: done(0,'unhealthy' if os.environ['FAIL_AT']=='health' else 'healthy')
  done(0,'true' if c[args[-1]] else 'false')
 if args[0]=='stop': c[args[-1]]=False; done()
 if args[0]=='rename':
  if args[2] in c: done(1)
  c[args[2]]=c.pop(args[1]); done()
 if args[0]=='rm': c.pop(args[-1],None); done()
 if args[0]=='start': c[args[-1]]=True; done()
 if args[0]=='run':
  if '--name' in args:
   assert not any(c.values()), 'Concurrent engines!'
   c[args[args.index('--name')+1]]=True
  done()
done(1)
'''

class DeployTests(unittest.TestCase):
    def scenario(self, failure):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root); bin_dir=root/'bin'; bin_dir.mkdir()
            for cmd in ('docker','sudo','caddy','systemctl','curl','sleep','chown'):
                path=bin_dir/cmd; path.write_text(FAKE); path.chmod(0o755)
            state_file=root/'state.json'
            state_file.write_text(json.dumps({'containers':{'kyhoi':True,'kyhoi-previous':False},'events':[]}))
            sites=root/'sites'; sites.mkdir(); fragment=sites/'kyhoi.shuneo.com.caddy'; fragment.write_text('previous ingress')
            env={**os.environ,'PATH':str(bin_dir)+os.pathsep+os.environ['PATH'],
                 'FAKE_STATE':str(state_file),'FAIL_AT':failure,'KYHOI_IMAGE':'kyhoi:'+'a'*40,
                 'CADDY_SITES_DIR':str(sites),'CADDY_CONFIG':str(root/'Caddyfile'),
                 'CADDY_LOCK_FILE':str(root/'ingress.lock'),'KYHOI_LOCK_FILE':str(root/'service.lock')}
            result=subprocess.run(['bash',str(Path(__file__).with_name('deploy.sh'))],env=env,capture_output=True,text=True,timeout=30)
            state=json.loads(state_file.read_text()); events=state['events']
            self.assertEqual(result.returncode,0 if failure=='none' else 1,result.stdout+result.stderr)
            self.assertTrue(state['containers']['kyhoi'])
            runs=[e for e in events if e[:2]==['docker','run']]
            if failure=='validate':
                self.assertFalse(runs)
                self.assertEqual(state['containers'],{'kyhoi':True,'kyhoi-previous':False})
            else:
                stop=next(i for i,e in enumerate(events) if e[:2]==['docker','stop'])
                backup=next(i for i,e in enumerate(events) if e[:2]==['docker','run'] and any('tar -czf' in a for a in e))
                start=next(i for i,e in enumerate(events) if e[:2]==['docker','run'] and '--name' in e)
                self.assertLess(stop,backup); self.assertLess(backup,start)
            if failure in ('health','https'):
                restore=next(i for i,e in enumerate(events) if any('tar -xzf' in a for a in e))
                restart=next(i for i,e in enumerate(events) if e[:2]==['docker','start'])
                self.assertLess(restore,restart)
            if failure!='none': self.assertEqual(fragment.read_text(),'previous ingress')
    def test_success(self): self.scenario('none')
    def test_invalid_ingress_preserves_running_engine(self): self.scenario('validate')
    def test_health_failure_restores_data_and_engine(self): self.scenario('health')
    def test_https_failure_restores_data_and_ingress(self): self.scenario('https')

if __name__=='__main__': unittest.main()
