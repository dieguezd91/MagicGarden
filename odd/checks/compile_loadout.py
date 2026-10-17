"""Compile project sources against existing Unity references without launching Editor.
This is a C# compiler check only, not Unity asset import or Play Mode validation.
Requires generated Unity csproj files, cached dependency assemblies and .NET SDK.
"""
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'Proyecto-Final'
OUT = ROOT / 'odd/checks/.build'
OUT.mkdir(exist_ok=True)
NS = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
sdk_lines = subprocess.check_output(['dotnet', '--list-sdks'], text=True).strip().splitlines()
version, base = sdk_lines[-1].split(' ', 1)
csc = Path(base.strip('[]')) / version / 'Roslyn/bincore/csc.dll'

def compile_project(name, extra=()):
    tree = ET.parse(PROJECT / (name + '.csproj'))
    assembly = OUT / (name + '.dll')
    arguments = ['/nologo', '/target:library', '/nostdlib+', '/langversion:9.0', '/out:' + str(assembly)]
    constants = tree.find('.//m:DefineConstants', NS)
    if constants is not None: arguments.append('/define:' + constants.text.replace(';', ','))
    sources = set()
    for item in tree.findall('.//m:Compile', NS): sources.add((PROJECT / item.attrib['Include']).resolve())
    sources.update(PROJECT / s for s in extra)
    for source in sorted(sources): arguments.append(str(source))
    for item in tree.findall('.//m:Reference/m:HintPath', NS):
        path = Path(item.text)
        if not path.is_absolute(): path = PROJECT / path
        if not path.exists(): raise RuntimeError('Unavailable reference: ' + str(path))
        arguments.append('/reference:' + str(path.resolve()))
    for item in tree.findall('.//m:ProjectReference', NS):
        dependency = Path(item.attrib['Include']).stem + '.dll'
        path = OUT / dependency
        if not path.exists(): path = PROJECT / 'Library/ScriptAssemblies' / dependency
        if not path.exists(): raise RuntimeError('Unavailable cached dependency: ' + str(path))
        arguments.append('/reference:' + str(path.resolve()))
    rsp = OUT / (name + '.rsp')
    rsp.write_text('\n'.join('"' + a + '"' for a in arguments), encoding='utf-8')
    print('C# compiler check: ' + name, flush=True)
    result = subprocess.run(['dotnet', str(csc), '@' + str(rsp)])
    if result.returncode: sys.exit(result.returncode)

compile_project('MagicGarden.Core', ['Assets/Scripts/Upgrades/Core/DailyLoadoutState.cs'])
compile_project('Assembly-CSharp', ['Assets/Scripts/Gameplay Systems/DailyLoadoutController.cs',
    'Assets/Scripts/UI/DailyLoadoutUI.cs', 'Assets/Scripts/UI/LoadoutSlotView.cs'])
compile_project('MagicGarden.Tests', ['Assets/Tests/Editor/Upgrades/DailyLoadoutTests.cs',
    'Assets/Tests/Editor/Upgrades/DailyLoadoutIntegrationTests.cs'])
print('PASS: core, gameplay and Editor-test sources compile against cached Unity references (not an Editor build).')
