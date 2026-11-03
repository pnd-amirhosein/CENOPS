"""Regression coverage for 2.52: visible PK/FK picker, selection clearing and shift keycap.

Run: python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
"""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
web = root / 'src/NTSW_CENOPS.WebUI'
js = web / 'wwwroot/js'

def bundle(name, helper=True):
    content = '\n'.join(line for line in (js / name).read_text().splitlines() if not line.startswith('import {')).replace('export function', 'function') if helper else '\n'.join(line for line in (js / name).read_text().splitlines() if not line.startswith('import {'))
    return content

script = '\n'.join([bundle('graph-interaction-support.js'), bundle('graph-paths.js'), bundle('graph-export.js').replace('export async function','async function'), bundle('database-inspector-graph.js', False)])
html = '''<!doctype html><html><head><meta charset="utf-8"><style>
body{margin:0}#root{position:relative!important;width:1050px;height:540px!important;min-height:0!important}
</style></head><body>
<div id="root" class="dbi-graph"><div class="graph-workspace">
 <div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div>
 <div class="graph-workspace-controls">
 <button type="button" class="graph-shift-action" disabled><svg viewBox="0 0 24 24"><path d="M3 10h18"/></svg><span class="graph-shift-badge"><svg viewBox="0 0 24 24"><path d="m12 3-9 9h5v8h8v-8h5l-9-9Z"/></svg></span></button>
 <button type="button" class="graph-shift-action"><svg viewBox="0 0 24 24"><path d="M3 10h18"/></svg><span class="graph-shift-badge"><svg viewBox="0 0 24 24"><path d="m12 3-9 9h5v8h8v-8h5l-9-9Z"/></svg></span></button>
 </div>
</div></div></body></html>'''

with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width': 1100, 'height': 800})
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.set_content(html)
    page.add_style_tag(content=(web / 'wwwroot/css/database-inspector.css').read_text())
    page.add_style_tag(content=(web / 'Components/Common/GraphWorkspace.razor.css').read_text())
    page.evaluate('''async text => {
      window.graph = await import(URL.createObjectURL(new Blob([text],{type:'text/javascript'})));
      window.root=document.querySelector('#root');
      window.events=[];
      graph.mount(root,[{id:1,level:0,schema:'dbo',name:'Orders',pkLoaded:false,fkLoaded:false}],[],
        {invokeMethodAsync:(name,...args)=>{events.push([name,...args]);return Promise.resolve()}},[1]);
    }''', script)
    card = page.locator('.dbi-node[data-node-id="1"]')
    assert card.count() == 1
    assert page.locator('.dbi-node.focus').count() == 0, 'dropdown seed is not a permanently focused card'
    card.locator('.dbi-node-heading').click()
    assert card.evaluate('(e)=>e.classList.contains("selected")')
    page.locator('.dbi-graph-stage').click(position={'x':960, 'y':340})
    assert not card.evaluate('(e)=>e.classList.contains("selected")'), 'blank stage should clear visible focus'

    page.keyboard.down('Shift')
    page.wait_for_timeout(160)
    assert page.locator('.graph-shift-badge').count() == 2
    badge = page.locator('.graph-shift-badge').first
    assert badge.evaluate('e=>getComputedStyle(e).visibility') == 'visible'
    assert badge.evaluate('e=>getComputedStyle(e).backgroundColor') == 'rgb(255, 255, 255)', 'keycap has white background'
    assert badge.locator('svg').evaluate('e=>getComputedStyle(e).strokeWidth') == '2.5px'
    page.keyboard.up('Shift')
    page.wait_for_timeout(160)
    assert badge.evaluate('e=>getComputedStyle(e).visibility') == 'hidden'

    # A genuine menu with many options; drag its anchor near the bottom of the stage.
    page.evaluate('''() => {
      const menu=document.createElement('div'); menu.className='dbi-fk-menu';
      const options=Array.from({length:16},(_,i)=>`<label><input type="checkbox">dbo.Table${i}</label>`).join('');
      menu.innerHTML=`<h3>انتخاب ارتباط‌های FK</h3><small>جداول مرتبط</small>
        <label class="dbi-select-all"><input type="checkbox">انتخاب همه</label>
        <div class="dbi-fk-options">${options}</div>
        <div class="dbi-fk-actions"><button>انصراف</button><button class="primary">نمایش والدها</button></div>`;
      root.querySelector('.graph-workspace').append(menu);
      root.querySelector('.dbi-pan').style.left='120px'; root.querySelector('.dbi-pan').style.top='325px';
    }''')
    for direction in (False, True):
        page.evaluate('primary=>{window.whichPrimary=primary;graph.positionMenu(root,1,primary)}', direction)
        values = page.evaluate('''() => {
          const container=root.getBoundingClientRect(),anchor=root.querySelector('.dbi-node-action.'+
             (window.whichPrimary?'pk':'fk')).getBoundingClientRect(),m=root.querySelector('.dbi-fk-menu').getBoundingClientRect();
          return {top:m.top-container.top,bottom:container.bottom-m.bottom,
            above:m.bottom<=anchor.top,width:m.width,height:m.height};
        }''')
        assert values['top'] >= 8, values
        assert values['bottom'] >= 8, values
        assert values['above'], 'menu should flip above PK/FK close to stage bottom: ' + str(values)
        assert values['height'] > 180

    # Very short stage: the entire menu stays in view and its content is scrollable.
    page.evaluate('''() => {
      root.style.setProperty('height','230px','important');root.style.setProperty('min-height','0','important');
      root.querySelector('.dbi-pan').style.left='40px'; root.querySelector('.dbi-pan').style.top='90px';
      graph.positionMenu(root,1,false);
    }''')
    dims = page.evaluate('''() => {
      const r=root.getBoundingClientRect(),m=root.querySelector('.dbi-fk-menu');const b=m.getBoundingClientRect();
      return {top:b.top-r.top,bottom:r.bottom-b.bottom,scroll:m.scrollHeight,client:m.clientHeight};
    }''')
    assert dims['top'] >= 8 and dims['bottom'] >= 8, dims
    assert dims['scroll'] >= dims['client'], dims
    assert not errors, errors
    browser.close()
    print('PASS: no permanent dropdown focus, blank-stage deselect, white Shift keycap (disabled too), PK/FK flip and constrained scrolling')
