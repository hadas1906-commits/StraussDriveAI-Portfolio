"""Start two loopback-only local servers. No account, key or cloud resource needed."""
import os
import argparse
from pathlib import Path
import signal
import subprocess
import time

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--frontend-port',type=int,default=5173)
parser.add_argument('--api-port',type=int,default=5080)
options=parser.parse_args();port=options.frontend_port;api_port=options.api_port
if any(not 1024 <= n <= 65535 for n in [port,api_port]) or port == api_port:parser.error('Choose distinct unprivileged local ports.')
environment={k:v for k,v in os.environ.items() if not k.startswith(('STRAUSS_AI_','OPENAI_','AZURE_OPENAI_','DEMO_GATE_')) and k not in ['Advisor__DemoRepresentativeCode','Advisor__DatabasePath']}
environment.update(PORTFOLIO_API_PORT=str(api_port),ASPNETCORE_ENVIRONMENT='Development',Advisor__DemoAuthEnabled='true',Advisor__ForceDeterministic='true',Advisor__DemoRepresentativeCode='',Advisor__DatabasePath=str(root/'.local/portfolio.db'),DEMO_GATE_ENABLED='false')
processes=[]
def stop(process,force=False):
    if os.name == 'nt':
        process.kill() if force else process.terminate()
    else:
        os.killpg(process.pid,signal.SIGKILL if force else signal.SIGTERM)
try:
    for command,cwd in [(['dotnet',str(root/'backend/StraussDrive.Api/bin/Debug/net10.0/StraussDrive.Api.dll'),'--contentRoot',str(root/'backend/StraussDrive.Api')],root),(['node',str(root/'frontend/node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port',str(port),'--strictPort'],root/'frontend')]:
        processes.append(subprocess.Popen(command,cwd=cwd,env=environment,start_new_session=os.name != 'nt'))
    print(f'Open http://127.0.0.1:{port} — deterministic fiction, no live AI. Ctrl+C stops both servers.',flush=True)
    while all(p.poll() is None for p in processes):time.sleep(.5)
    raise SystemExit(f'A local server exited; check the preceding output. Ports {api_port} and {port} must be free.')
except KeyboardInterrupt:pass
finally:
    for process in processes:
        if process.poll() is None:stop(process)
    for process in processes:
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:stop(process,True);process.wait()
