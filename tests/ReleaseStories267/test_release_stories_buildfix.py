"""Regression checks for GLASSWORK 2.67 C# / Razor compilation failures.

A full Razor/C# compile still requires the .NET SDK. These assertions make it
harder to accidentally reintroduce the same namespace and type-name mistakes.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
read = lambda relative: (root / relative).read_text(encoding='utf-8')

contracts = read('src/NTSW_CENOPS.Application/Stories/IReleaseStoryService.cs')
service = read('src/NTSW_CENOPS.Infrastructure/Stories/ReleaseStoryService.cs')
viewer = read('src/NTSW_CENOPS.WebUI/Components/Stories/ReleaseStoryViewer.razor')
file_access = read('src/NTSW_CENOPS.Infrastructure/Files/FileResourceAccess.cs')
keys = read('src/NTSW_CENOPS.Domain/Authorization/AccessKeys.cs')

# Keep the DTO name separate from the identically-named Razor component.
assert re.search(r'\b(?:sealed\s+)?record\s+ReleaseStoryViewerInfo\s*\(\s*int\s+UserId\s*,\s*string\s+DisplayName\s*,\s*string\s+Username\s*,\s*DateTimeOffset\s+ViewedAt', contracts)
assert not re.search(r'\brecord\s+ReleaseStoryViewer\s*\(', contracts)
assert 'Task<IReadOnlyList<ReleaseStoryViewerInfo>> ViewersAsync(' in contracts
assert 'Task<IReadOnlyList<ReleaseStoryViewerInfo>> ViewersAsync(' in service
assert 'select new ReleaseStoryViewerInfo(' in service
assert 'private List<ReleaseStoryViewerInfo> _viewers = [];' in viewer
assert 'List<ReleaseStoryViewer>' not in viewer
for property_name in ('UserId', 'DisplayName', 'Username', 'ViewedAt'):
    assert f'viewer.{property_name}' in viewer

# Permission lookup from Infrastructure must import the Domain authorization constants.
assert 'using NTSW_CENOPS.Domain.Authorization;' in file_access
assert 'namespace NTSW_CENOPS.Domain.Authorization;' in keys
assert 'const string Chat = ' in keys
assert 'access.HasAccessAsync(actor, AccessKeys.Chat, ct)' in file_access
print('PASS: 2.67 C# story DTO naming and FileResourceAccess namespace contracts')
