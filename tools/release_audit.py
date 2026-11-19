"""Read-only scoped audit of Git-reviewed release files. Never prints matched values.
Heuristics are not proof that credentials are absent; no remote secret validation occurs.
"""
from pathlib import Path
import subprocess
import re
import json
import sys

root=Path(__file__).resolve().parents[1]
result=subprocess.run(['git','ls-files','-z'],cwd=root,capture_output=True,check=True)
files=[root/p for p in result.stdout.decode().split('\0') if p]
if not files:raise SystemExit('Stage reviewed files in the fresh local repository before auditing.')
patterns={
 'provider-token':re.compile(rb'\b(?:sk-(?:proj-)?[A-Za-z0-9_-]{24,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[A-Z0-9]{16}|AIza[A-Za-z0-9_-]{30,}|xox[baprs]-[A-Za-z0-9-]{15,})\b'),
 'private-key':re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----'),
 'credential-url':re.compile(rb'(?i)\b[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]+@'),
 'production-endpoint':re.compile(rb'(?i)up\.railway\.app|api\.openai\.com|openai\.azure\.com'),
 'connection-password':re.compile(rb'(?i)(?:Server|Host|Data Source)\s*=.{0,160}(?:Password|Pwd)\s*='),
}
failures=[];image_count=0;assignments=[]
fixture_paths={'frontend/playwright.config.ts','tests/StraussDrive.Tests/DemoGateTests.cs','tests/StraussDrive.Tests/RepresentativeGateTests.cs','tests/StraussDrive.Tests/PortfolioTests.cs'}
for file in files:
 relative=file.relative_to(root).as_posix()
 if file.suffix.lower() in {'.pdf','.xlsx','.csv','.db','.sqlite','.sqlite3','.pem','.key','.pfx','.p12'} or file.name.startswith('.env'):
  failures.append((relative,'forbidden-source-or-sensitive-file'))
 if any(p in file.parts for p in ['node_modules','bin','obj','.local','dist']):failures.append((relative,'generated/private-directory'))
 b=file.read_bytes()
 if b.startswith(b'\x89PNG'):
  image_count+=1
  # OCR supplements source review; only matches, never recognized text, are reported.
  p=subprocess.run(['tesseract',str(file),'stdout','-l','eng'],capture_output=True) if subprocess.run(['which','tesseract'],capture_output=True).returncode==0 else None
  if p and p.returncode==0:b=p.stdout
 elif b'\0' in b[:8192]:failures.append((relative,'unreviewed-binary'));continue
 for label,pattern in patterns.items():
  if pattern.search(b):failures.append((relative,label))
 # Inspect literal assignments without exposing values. Only explicit test fixtures may contain them.
 for m in re.finditer(rb'''(?i)(?:PASSWORD|API_KEY|DemoRepresentativeCode)["']?(?:\])?\s*[:=]\s*["']([^"'\r\n]*)["']''',b):
  literal=m.group(1)
  if not literal:continue
  if relative in fixture_paths and any(marker in literal.lower() for marker in [b'synthetic',b'test-',b'wrong',b'syn',b'role']):assignments.append(relative)
  elif literal.startswith(b'[') or literal==b'true':continue
  else:failures.append((relative,'credential-literal-needs-review'))
 # Original challenge personal identifiers/financial expectations must not be carried forward.
 if relative not in {'THIRD_PARTY_NOTICES.md','tools/release_audit.py'} and re.search(rb'\b(?:E-00[1-6]|CAR-19|CARNIVAL|NIRO|COROLLA)\b',b):failures.append((relative,'excluded-challenge-identity'))
 if relative.startswith('data/') and re.search(rb'(?i)"(?:email|phone|nationalId|identityNumber|password|token)"\s*:',b):failures.append((relative,'personal-or-secret-data-field'))
data=json.loads((root/'data/portfolio-demo.json').read_text())
if len(data['employees'])!=5 or len(data['vehicles'])!=6:failures.append(('data/portfolio-demo.json','unexpected-fixture-scope'))
remote=subprocess.check_output(['git','remote'],cwd=root).decode().strip()
print('Reviewed tracked files:',len(files),'PNG files:',image_count,'synthetic fixture assignment files:',len(set(assignments)))
print('External Git remote configured:',bool(remote))
for path,category in sorted(set(failures)):print(category+': '+path)
print('Result:', 'NEEDS REVIEW' if failures else 'PASS — no matching secret/privacy blockers in this scoped scan')
sys.exit(bool(failures))
