"""Dependency-free static project checks; NOT a substitute for .NET Windows compilation."""
from pathlib import Path
import xml.etree.ElementTree as ET
import re

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src' / 'MediaDesk'
files = list(SRC.rglob('*'))
for path in files:
    if path.suffix in ('.xaml', '.csproj'):
        ET.parse(path)
        print('XML PASS', path.relative_to(ROOT))

for window in ['App', 'MainWindow', 'PlayerWindow']:
    markup = (SRC / (window + '.xaml')).read_text(encoding='utf-8')
    code = (SRC / (window + '.xaml.cs')).read_text(encoding='utf-8')
    for event_handler in re.findall(r'\b(?:Click|Closing|KeyDown|MouseDoubleClick|MediaOpened|MediaFailed)="([A-Za-z_]\w*)"', markup):
        assert re.search(r'\b' + event_handler + r'\s*\(', code), f'missing handler {window}.{event_handler}'
    print('EVENTS PASS', window)

project = (SRC/'MediaDesk.csproj').read_text(encoding='utf-8')
assert '<UseWPF>true</UseWPF>' in project
assert '<TargetFramework>net10.0-windows</TargetFramework>' in project
assert (SRC/'media.ico').exists()  # bundled original icon
assert 'PublishSingleFile=true' in (ROOT/'build.ps1').read_text(encoding='utf-8')
print('STATIC CHECKS PASSED; executable compilation still required on Windows')
