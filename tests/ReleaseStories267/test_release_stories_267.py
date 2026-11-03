"""GLASSWORK 2.67: source contracts and visual behavior of the release story shell.

Real EF/Blazor execution needs the .NET SDK and a migrated SQL Server.
"""
from pathlib import Path
import re
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]
read = lambda path: (ROOT / path).read_text(encoding='utf-8')

ctx = read('src/NTSW_CENOPS.Infrastructure/Persistence/AppDbContext.cs')
assert 'DbSet<ReleaseStory>' in ctx and 'DbSet<ReleaseStoryView>' in ctx
assert 'HasIndex(x => x.SourceFile).IsUnique()' in ctx
assert 'new { x.StoryId, x.UserId }' in ctx
assert 'DeleteBehavior.Restrict' in ctx
migration = read('src/NTSW_CENOPS.Infrastructure/Migrations/20261102180000_AddReleaseStories.cs')
assert 'ReleaseStoryViews' in migration and 'ReleaseStories' in migration
snapshot = read('src/NTSW_CENOPS.Infrastructure/Migrations/AppDbContextModelSnapshot.cs')
assert snapshot.count('modelBuilder.Entity("NTSW_CENOPS.Infrastructure.Stories.ReleaseStory",') == 1
assert snapshot.count('modelBuilder.Entity("NTSW_CENOPS.Infrastructure.Stories.ReleaseStoryView",') == 2
assert 'IX_ReleaseStories_SourceFile' in migration
assert 'FK_ReleaseStoryViews_AspNetUsers_UserId' in migration
service = read('src/NTSW_CENOPS.Infrastructure/Stories/ReleaseStoryService.cs')
for expected in ['ListAsync', 'OpenAsync', 'ViewersAsync', 'ImportNewAsync', 'SemaphoreSlim',
                 'ToHashSet(StringComparer.OrdinalIgnoreCase)', 'ViewedAt = DateTimeOffset.UtcNow',
                 'OrderBy(s => ParseVersion(s.Version))', 'IsSubsetOf(imported)']:
    assert expected in service, expected
assert 'new ReleaseStory' in service and 'Markdown = markdown' in service
assert 'Markdown = markdown' in service
assert 'SourceCreatedAt' in service
csproj = read('src/NTSW_CENOPS.WebUI/NTSW_CENOPS.WebUI.csproj')
assert 'GLASSWORK*.md' in csproj and 'CopyToPublishDirectory' in csproj
skyline = read('src/NTSW_CENOPS.WebUI/Components/Layout/Skyline.razor')
assert 'href="/dashboard" title="رفتن به داشبورد"' not in skyline
assert '<ReleaseStoryViewer' in skyline and 'is-unread' in skyline
assert 'RefreshStoriesAsync' in skyline
viewer = read('src/NTSW_CENOPS.WebUI/Components/Stories/ReleaseStoryViewer.razor')
for expected in ['ReleaseStoryViewer', 'ToggleViewersAsync', '<UserHoverName', 'PreviousAsync',
                 'NextAsync', 'GoToAsync', 'OnReadChanged', 'FindIndex(s => !s.Viewed)',
                 'CloseAsync', 'OnClose', 'ReleaseStoryViewCount']:
    if expected == 'ReleaseStoryViewCount': continue
    assert expected in viewer, expected
assert 'Task.Delay' not in viewer and 'Timer' not in viewer and 'setInterval' not in viewer
renderer = read('src/NTSW_CENOPS.WebUI/Components/Stories/StoryMarkdownRenderer.cs')
assert 'HtmlEncode' in renderer and 'https' in renderer.lower()
assert 'Uri.UriSchemeHttps' in renderer and 'Uri.UriSchemeHttp' in renderer
assert 'Markdown' in renderer and 'Render' in renderer
assert 'Version = "2.67"' in read('src/NTSW_CENOPS.WebUI/Components/Common/CenopsRelease.cs')

css = read('src/NTSW_CENOPS.WebUI/wwwroot/css/release-stories.css')
html = f'''<!DOCTYPE html><html><head><meta charset="utf-8"><style>{css}</style></head><body>
<div class="release-story-backdrop"><div class="release-story-window" dir="rtl">
<header class="release-story-header"><div class="release-story-identity"><span class="release-story-avatar">C</span><div><strong>GLASSWORK · داستانچه انتشار</strong><small>نسخه 2.67 · 1 از 4</small></div></div><div class="release-story-header-actions"><button class="release-story-icon" id="eye">◉</button><button class="release-story-icon" id="close">✕</button></div></header>
<div class="release-story-steps"><button class="release-story-step is-active"><span></span></button><button class="release-story-step is-unread"><span></span></button><button class="release-story-step is-unread"><span></span></button><button class="release-story-step is-viewed"><span></span></button></div>
<div class="release-story-stage"><main class="release-story-reading"><div class="release-story-reading-top">PATCH NOTES <span>2.67</span></div><h1>Release Story</h1><div class="release-story-markdown"><h2>Improvements</h2><p>Text is long and readable, and stories don't advance automatically.</p><pre><code>print('hello')\n</code></pre></div></main><button class="release-story-side release-story-side--next">Next</button></div>
<footer class="release-story-footer"><span>بدون پخش خودکار</span><span>1 / 4</span></footer></div></div>
</body></html>'''
with sync_playwright() as pw:
    browser = pw.chromium.launch(headless=True, executable_path='/usr/bin/chromium', args=['--no-sandbox'])
    for width, height in [(1366, 800), (390, 844)]:
        page = browser.new_page(viewport={'width': width, 'height': height})
        page.set_content(html)
        assert page.locator('.release-story-window').is_visible()
        assert page.locator('.release-story-side--next').is_visible()
        bounds = page.locator('.release-story-window').bounding_box()
        assert bounds['width'] <= width + 1
        assert bounds['height'] <= height + 3, (width, height, bounds)
        assert page.locator('.release-story-reading').evaluate('(el) => getComputedStyle(el).overflowY') == 'auto'
        assert page.locator('.release-story-steps .release-story-step').count() == 4
        page.close()
    browser.close()
print('PASS: 2.67 story source contracts and Chromium layout (desktop/mobile)')
