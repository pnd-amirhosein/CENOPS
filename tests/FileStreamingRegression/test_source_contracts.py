"""Quick source/solution sanity checks; runtime FileStreaming tests require dotnet."""
from pathlib import Path
from xml.etree import ElementTree as ET

root = Path(__file__).resolve().parents[2]
projects = {p.stem: p for p in (root / 'src').glob('*/*.csproj')}
assert set(projects) == {
    'NTSW_CENOPS.Domain', 'NTSW_CENOPS.Application',
    'NTSW_CENOPS.Infrastructure', 'NTSW_CENOPS.WebUI',
    'NTSW_CENOPS.FileStreaming',
}
solution = (root / 'NTSW_CENOPS.sln').read_text()
for project, csproj in projects.items():
    assert f'= "{project}"' in solution
    for ref in ET.parse(csproj).findall('.//ProjectReference'):
        assert (csproj.parent / ref.attrib['Include'].replace('\\', '/')).is_file()

file_csproj = ET.parse(projects['NTSW_CENOPS.FileStreaming'])
assert not file_csproj.findall('.//ProjectReference')
for p in (root / 'src').rglob('*'):
    assert 'HealthMonitoringApp' not in p.name
    if p.suffix in ('.cs', '.razor', '.csproj'):
        assert 'HealthMonitoringApp' not in p.read_text(encoding='utf-8')

web_project = (root / 'src/NTSW_CENOPS.WebUI/NTSW_CENOPS.WebUI.csproj').read_text()
app = (root / 'src/NTSW_CENOPS.WebUI/Components/App.razor').read_text()
assert '<AssemblyName>NTSW_CENOPS.WebUI</AssemblyName>' in web_project
assert 'NTSW_CENOPS.WebUI.styles.css' in app
assert 'AddCenopsFileStreaming' not in (root / 'src/NTSW_CENOPS.WebUI/Program.cs').read_text()
assert 'NTSW_CENOPS.FileStreaming' not in web_project
storage = (root / 'src/NTSW_CENOPS.FileStreaming/Storage/LocalFileStorage.cs').read_text()
assert 'ReadAsync' in storage and 'WriteAsync' in storage and 'File.Move(temporary, destination)' in storage
assert 'FileSizeLimitExceededException' in storage
print('PASS: rename, solution wiring, independent module and basic storage boundaries')
