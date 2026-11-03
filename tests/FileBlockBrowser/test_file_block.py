"""File upload UI and single-adapter regression. Python/Chromium, no .NET runtime necessary."""
from pathlib import Path
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
web = root / 'src/NTSW_CENOPS.WebUI'
app = root / 'src/NTSW_CENOPS.Application'
infra = root / 'src/NTSW_CENOPS.Infrastructure'

insert = (web / 'Components/Notes/NoteInsertMenu.razor').read_text()
editor = (web / 'Components/Notes/NoteEditor.razor').read_text()
reader = (web / 'Components/Notes/NoteContent.razor').read_text()
service = (infra / 'Notes/NoteService.cs').read_text()
block = (web / 'Components/Files/AppFileBlock.razor').read_text()
gateway = (app / 'Files/FileContracts.cs').read_text()
adapter = (infra / 'Files/AppFileGateway.cs').read_text()
startup = (infra / 'DependencyInjection.cs').read_text()
endpoints = (web / 'Endpoints/FileEndpoints.cs').read_text()

assert 'NoteBlockKind.File' in insert and '"فایل"' in insert
assert '<AppFileBlock @bind-Value="block.Content" Mode="FileBlockMode.Edit" NoteId="_model.Id" />' in editor
assert '<AppFileBlock Value="@block.Content" Mode="FileBlockMode.View" NoteId="NoteId" />' in reader
assert 'FileReferenceCodec.IsValid' in service
assert 'interface IAppFileGateway' in gateway
assert 'class AppFileGateway' in adapter
assert 'AddScoped<IAppFileGateway, AppFileGateway>()' in startup
assert 'MapFileEndpoints()' in (web / 'Program.cs').read_text()
assert 'RequireAuthorization()' in endpoints
assert 'AppModal IsOpen="_deleteCandidate is not null"' in block
assert 'AppModal IsOpen="_preview is not null"' in block
assert 'FileGateway.Check(file.Name, file.Size)' in block
assert 'FileViewMode.List' in block and 'FileViewMode.Gallery' in block
assert 'FileGateway.DeleteAsync' in block
assert 'interface IFileCheckService' in gateway
assert 'class FileCheckService' in (infra / 'Files/FileCheckService.cs').read_text()
assert 'FileReferenceCodec' in block and 'NoteFileReferences' not in block
assert 'FileGateway.OpenRead' not in block  # browser delivery is the gateway-backed endpoint
assert 'JsonSerializer.Serialize(ids)' in (app / 'Files/FileReferenceCodec.cs').read_text()
for csfile in web.rglob('*.cs'):
    assert 'NTSW_CENOPS.FileStreaming' not in csfile.read_text(), csfile
for razor in web.rglob('*.razor'):
    assert 'NTSW_CENOPS.FileStreaming' not in razor.read_text(), razor
for csfile in app.rglob('*.cs'):
    assert 'NTSW_CENOPS.FileStreaming' not in csfile.read_text(), csfile

css = (web / 'wwwroot/css/cenops-file-block.css').read_text()
js = (web / 'wwwroot/js/file-drop.js').read_text()
html = '''<html lang="fa" dir="rtl"><head><meta charset="utf-8"><style>
*{box-sizing:border-box}body{background:#e6f0f2;font:13px Tahoma,Arial}
'''+css+'''</style></head><body><main style="width:min(600px,90vw);margin:80px auto">
<div class="cenops-file-block"><div class="cenops-file-drop" id="drop">
<svg viewBox="0 0 24 24"><path d="M12 5v14"/></svg><strong>فایل‌ها را اینجا رها کنید یا برای انتخاب کلیک کنید</strong>
<span>می‌توانید چند فایل انتخاب کنید</span><input class="cenops-file-native-input" type="file" multiple>
</div><div class="cenops-file-queue"><div class="cenops-file-pending-list"><span class="cenops-file-pending"><span class="cenops-file-mini-icon">PNG</span><span>image.png</span><span>30 KB</span></span></div><button class="cenops-file-upload-button">بارگذاری</button></div>
<div class="cenops-file-uploaded-list"><div class="cenops-file-square"><button class="cenops-file-square-main"><span class="cenops-file-large-icon">PDF</span><span class="cenops-file-square-name">document.pdf</span></button><button class="cenops-file-remove">×</button></div></div></div>
</main></body></html>'''
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width':980, 'height':720})
    page.set_content(html)
    page.evaluate(js)
    page.evaluate('cenopsFileDrop.attach(document.getElementById("drop"))')
    page.evaluate('cenopsFileDrop.attach(document.getElementById("drop"))')
    assert page.locator('#drop').get_attribute('data-file-drop-attached') == 'true'
    page.evaluate('''() => {
      window.fileChanges = 0;
      document.querySelector('input[type=file]').addEventListener('change', () => window.fileChanges++);
      const dt = new DataTransfer();
      dt.items.add(new File(['test'], 'myfile.txt', {type:'text/plain'}));
      const el = document.getElementById('drop');
      const enter = new DragEvent('dragenter', { bubbles:true, cancelable:true, dataTransfer:dt });
      el.dispatchEvent(enter);
      if (!el.classList.contains('is-over')) throw Error('missing drag highlight');
      const drop = new DragEvent('drop', { bubbles:true, cancelable:true, dataTransfer:dt });
      el.dispatchEvent(drop);
    }''')
    assert page.evaluate('window.fileChanges') == 1
    assert page.evaluate('document.querySelector("input[type=file]").files[0].name') == 'myfile.txt'
    assert not page.locator('#drop').evaluate('e=>e.classList.contains("is-over")')
    assert page.locator('.cenops-file-queue').bounding_box()['height'] <= 40
    assert page.locator('.cenops-file-square').bounding_box()['width'] == 94
    page.set_viewport_size({'width':390,'height':760})
    assert page.locator('.cenops-file-drop').bounding_box()['width'] <= 390
    browser.close()
print('PASS: file block contracts, single gateway boundary, and Chromium drag/drop + compact visuals')
