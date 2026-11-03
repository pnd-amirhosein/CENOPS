"""GLASSWORK 2.65: file block structure and Chromium layout/retained input regression."""
from pathlib import Path
import tinycss2
from playwright.sync_api import sync_playwright

root=Path(__file__).resolve().parents[2]
web=root/'src/NTSW_CENOPS.WebUI'
block=(web/'Components/Files/AppFileBlock.razor').read_text()
viewer=(web/'Components/Files/AppFilePreview.razor').read_text()
css=(web/'wwwroot/css/cenops-file-block.css').read_text()
js=(web/'wwwroot/js/file-drop.js').read_text()

assert 'MaxQueuedFiles' in block and '_pending.AddRange(' in block
assert '_pending = args.GetMultipleFiles' not in block
assert '_pickerKeys.Add(Guid.NewGuid())' in block and 'PruneUnusedPickers()' in block
assert 'pending.BatchId == key' in block
assert 'class="@GetPickerCssClass(pickerKey)"' in block
assert 'cenops-file-native-input cenops-file-native-input--active' in block
assert 'cenops-file-native-input cenops-file-native-input--retained' in block
assert '.cenops-file-native-input--active' in js
assert 'GetMultipleFiles(MaxBatchSize)' in block and 'MaxQueuedFiles' in block
assert 'ListPageSize' in block and 'Skip((_listPage - 1) * SafePageSize).Take(SafePageSize)' in block
assert 'SetPage(_listPage - 1)' in block and 'SetPage(_listPage + 1)' in block
assert '@ondblclick="() => Preview(file)"' in block
assert '@ondblclick="() => Preview(selected)"' in block
assert '@onclick="() => Preview(file)"' in block  # only for editor uploaded-file squares
assert 'cenops-file-gallery-open' not in block and 'CanPreviewImage' not in block
assert '<AppFilePreview File="selected"' in block and '<AppFilePreview File="_preview"' in block
assert '<img ' not in block # no eager full-content thumbnails
assert 'FileGateway.FindAsync(id)' in block and 'FileGateway.PreviewAsync(' not in block
assert 'FileGateway.PreviewAsync(File.Id)' in viewer
assert 'MaxWidth="min(96vw, 1500px)"' in block
assert 'CssClass="cenops-file-preview-dialog"' in block
assert 'font-family:inherit' in css and 'cenops-file-pending-remove svg' in css
errors=[e for e in tinycss2.parse_stylesheet(css,skip_whitespace=True,skip_comments=True) if e.type=='error']
assert not errors, errors

html=f'''<!doctype html><html lang="fa" dir="rtl"><meta charset="utf-8"><style>
*{{box-sizing:border-box}}body{{margin:20px;background:#eaf2f5;font:13px Tahoma,Arial}}
{css}</style><body><main style="max-width:650px;margin:auto">
<div class="cenops-file-block">
<div class="cenops-file-drop" id="drop"><strong>آپلود فایل</strong><input type="file" multiple class="cenops-file-native-input cenops-file-native-input--retained" id="older"><input type="file" multiple class="cenops-file-native-input cenops-file-native-input--active" id="current"></div>
<div class="cenops-file-view-canvas" id="canvas">
<section class="cenops-file-list-pane" id="list"><div class="cenops-file-view-list">{''.join(f'<button class="cenops-file-view-row"><span class="cenops-file-row-icon">PDF</span><span>file{i}.pdf</span><span>2 KB</span><span>PDF</span></button>' for i in range(7))}</div>
<nav class="cenops-file-pagination"><span class="cenops-file-page-summary">15 فایل · صفحه 1 از 3</span><div class="cenops-file-page-actions"><button>‹</button><button class="active">1</button><button>2</button><button>3</button><button>›</button></div></nav></section>
<section class="cenops-file-gallery" id="gallery" style="display:none"><div class="cenops-file-gallery-main"><div class="cenops-file-preview cenops-file-preview--compact"><div class="cenops-file-preview-state">پیش نمایش</div></div></div><div class="cenops-file-gallery-caption">sample.pdf</div><div class="cenops-file-gallery-strip"><button class="cenops-file-gallery-tile active">PDF</button><button class="cenops-file-gallery-tile">PNG</button></div></section>
</div><div class="cenops-file-queue"><div class="cenops-file-pending-list"><span class="cenops-file-pending"><span class="cenops-file-mini-icon">PNG</span><span class="cenops-file-truncate">report.png</span><button class="cenops-file-pending-remove" aria-label="remove"><svg viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg></button></span></div><button class="cenops-file-upload-button"><span class="cenops-file-upload-count">2</span>بارگذاری</button></div>
</div></main></body></html>'''

with sync_playwright() as p:
  browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
  page=browser.new_page(viewport={'width':1100,'height':800})
  page.set_content(html)
  page.evaluate(js)
  page.evaluate('cenopsFileDrop.attach(document.getElementById("drop"))')
  page.evaluate('''() => {
    window.events=[];
    document.querySelectorAll('input[type=file]').forEach(i=>i.addEventListener('change',()=>window.events.push(i.id)));
    const old=document.querySelector('#older');
    const oldFiles=new DataTransfer();oldFiles.items.add(new File(['abc'],'earlier.txt'));
    old.files=oldFiles.files;
    const second=new DataTransfer();second.items.add(new File(['def'],'latest.txt'));
    document.getElementById('drop').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:second}));
  }''')
  assert page.evaluate('events')==['current']
  assert page.locator('#older').evaluate('i=>i.files[0].name')=='earlier.txt'
  assert page.locator('#current').evaluate('i=>i.files[0].name')=='latest.txt'
  assert page.locator('#older').evaluate('i=>getComputedStyle(i).pointerEvents')=='none'
  assert page.locator('.cenops-file-view-row').count()==7
  list_height=page.locator('#list').bounding_box()['height']
  page.evaluate("document.querySelector('#list').style.display='none';document.querySelector('#gallery').style.display='flex'")
  gallery_height=page.locator('#gallery').bounding_box()['height']
  assert abs(list_height-gallery_height)<1,(list_height,gallery_height)
  assert 320<=gallery_height<=365,gallery_height
  assert page.locator('.cenops-file-gallery-main').bounding_box()['height']<300
  assert page.locator('.cenops-file-gallery-open').count()==0
  assert page.locator('.cenops-file-pending-remove svg').count()==1
  page.set_viewport_size({'width':390,'height':760})
  assert page.locator('#gallery').bounding_box()['height']==page.locator('#canvas').bounding_box()['height']
  browser.close()
print('PASS: 2.65 pagination/lazy preview contracts, persistent file picker, symmetric canvas, icon and compact queue')
