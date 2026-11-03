"""File preview contract and responsive UI tests. Does not require .NET runtime."""
from pathlib import Path
from playwright.sync_api import sync_playwright
import tinycss2

root = Path(__file__).resolve().parents[2]
web = root/'src/NTSW_CENOPS.WebUI'
files = root/'src/NTSW_CENOPS.FileStreaming'
app = root/'src/NTSW_CENOPS.Application'
infra = root/'src/NTSW_CENOPS.Infrastructure'
block = (web/'Components/Files/AppFileBlock.razor').read_text()
viewer = (web/'Components/Files/AppFilePreview.razor').read_text()
reader = (files/'Services/FilePreviewReader.cs').read_text()
contracts = (app/'Files/FileContracts.cs').read_text()
endpoint = (web/'Endpoints/FileEndpoints.cs').read_text()
css = (web/'wwwroot/css/cenops-file-block.css').read_text()

assert '<AppFilePreview File="selected"' in block
assert '<AppFilePreview File="_preview"' in block
assert 'RemovePending(pending.Id)' in block
assert '_pending.Count(x => x.Error is null)' in block
assert 'class="cenops-file-pending-remove"' in block
assert 'FileGateway.PreviewAsync(File.Id)' in viewer
assert 'new AppFilePreviewData' in viewer
assert 'IFilePreviewReader' in (files/'DependencyInjection.cs').read_text()
assert 'IAppFileGateway' in viewer
assert 'PreviewAsync(Guid id' in contracts
assert 'previewReader.ReadAsync' in (infra/'Files/AppFileGateway.cs').read_text()
assert 'FilePreviewReader' in reader and 'ReadWorkbook' in reader and 'ReadOfficeText' in reader
assert 'DtdProcessing.Prohibit' in reader and 'MaxXmlEntryBytes' in reader
for ext in ['.xlsx','.docx','.pptx','.cs','.css','.html','.cshtml','.js','.xml','.csv','.svg']:
    assert f'"{ext}"' in reader, ext
assert '"application/pdf"' in endpoint and '"%PDF-"u8' in endpoint
assert 'RequireAuthorization()' in endpoint
assert 'MarkupString' not in viewer  # Never render uploaded HTML/SVG as executable DOM
for source in [*web.rglob('*.razor'),*web.rglob('*.cs'),*app.rglob('*.cs')]:
    assert 'NTSW_CENOPS.FileStreaming' not in source.read_text(), source
errors = [t for t in tinycss2.parse_stylesheet(css,skip_whitespace=True,skip_comments=True) if t.type == 'error']
assert not errors, errors

html = f'''<!doctype html><html lang="fa" dir="rtl"><meta charset="utf-8"><style>
*{{box-sizing:border-box}}body{{font:13px Tahoma,Arial; margin:20px;background:#eaf2f5}}
{css}
</style><body><main style="width:min(650px,100%);margin:auto">
<div class="cenops-file-block">
<div class="cenops-file-view-canvas"><section class="cenops-file-gallery"><div class="cenops-file-gallery-main">
<div class="cenops-file-preview cenops-file-preview--compact" id="preview">
<img class="cenops-file-preview-image" src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='640' height='360'%3E%3Crect fill='%2307a4bf' width='640' height='360'/%3E%3C/svg%3E" /></div>

</div><div class="cenops-file-gallery-caption">sample.png</div>
<div class="cenops-file-gallery-strip"><button class="cenops-file-gallery-tile active"><span>PDF</span></button></div>
</section></div>
<div class="cenops-file-queue"><div class="cenops-file-pending-list">
<span class="cenops-file-pending"><span class="cenops-file-mini-icon">CS</span><span class="cenops-file-truncate">Program.cs</span><span>5 KB</span><button class="cenops-file-pending-remove" title="remove">×</button></span>
</div><button class="cenops-file-upload-button"><span class="cenops-file-upload-count">3</span>بارگذاری</button></div>
<div class="cenops-file-modal-preview"><div class="cenops-file-preview"><pre class="cenops-file-preview-text">&lt;script&gt;doNotExecute()&lt;/script&gt;</pre></div></div>
</div></main></body></html>'''

with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page(viewport={'width':900,'height':900})
    page.set_content(html)
    assert page.locator('.cenops-file-gallery').evaluate('x=>getComputedStyle(x).borderTopWidth') == '0px'
    assert page.locator('.cenops-file-gallery-tile.active').evaluate('x=>getComputedStyle(x).outlineStyle') == 'none'
    preview = page.locator('#preview').bounding_box()
    image = page.locator('.cenops-file-preview-image').bounding_box()
    assert preview['width'] == image['width'] and preview['height'] == image['height']
    assert page.locator('.cenops-file-pending-remove').evaluate('x=>getComputedStyle(x).opacity') == '0'
    page.locator('.cenops-file-pending').hover()
    page.wait_for_timeout(240)
    assert float(page.locator('.cenops-file-pending-remove').evaluate('x=>getComputedStyle(x).opacity')) > .9
    assert page.locator('.cenops-file-queue').bounding_box()['height'] <= 40
    assert page.locator('.cenops-file-upload-count').inner_text() == '3'
    assert page.locator('.cenops-file-preview-text').inner_text() == '<script>doNotExecute()</script>'
    assert page.locator('.cenops-file-preview-text script').count() == 0
    page.set_viewport_size({'width':390,'height':760})
    assert page.locator('.cenops-file-gallery-main').bounding_box()['width'] < 390
    assert page.locator('#preview').bounding_box()['height'] >= 220
    browser.close()
print('PASS: file preview contracts, gallery sizing, queue hover, and markup escaping')
