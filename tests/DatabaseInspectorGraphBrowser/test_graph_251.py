"""Glasswork 2.51 interaction contract. Run: python tests/DatabaseInspectorGraphBrowser/test_graph_251.py"""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
web = root / 'src/NTSW_CENOPS.WebUI'
js = web / 'wwwroot/js'
def local_module(name):
    source = (js / name).read_text()
    return '\n'.join(line for line in source.splitlines() if not line.startswith('import {'))

script = '\n'.join([
    local_module('graph-interaction-support.js').replace('export function', 'function'),
    local_module('graph-paths.js').replace('export function', 'function'),
    local_module('graph-export.js').replace('export async function','async function').replace('export function','function'),
    local_module('database-inspector-graph.js')
])
html = '''<!DOCTYPE html><html><head><meta charset="utf-8"><style>
body{margin:0}#root{position:relative;width:1300px;height:800px;}
</style></head><body><div id="root" class="dbi-graph"><div class="graph-workspace">
<div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div>
<div class="graph-workspace-controls"><button class="graph-shift-action">Path<span class="graph-shift-badge"><svg viewBox="0 0 24 24"><path d="m12 3-9 9h5v8h8v-8h5l-9-9Z"/></svg></span></button>
<button class="graph-shift-action">Focus<span class="graph-shift-badge"><svg viewBox="0 0 24 24"><path d="m12 3-9 9h5v8h8v-8h5l-9-9Z"/></svg></span></button></div>
</div></div></body></html>'''

with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width': 1400, 'height': 950})
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.set_content(html)
    for path in [web/'wwwroot/css/database-inspector.css', web/'Components/Common/GraphWorkspace.razor.css']:
        page.add_style_tag(content=path.read_text())
    page.add_style_tag(content='#root{height:800px!important;min-height:800px!important;}')
    page.evaluate('''async code => {
      window.graph = await import(URL.createObjectURL(new Blob([code],{type:'text/javascript'})));
      window.root = document.getElementById('root');
      window.initial = [
        {id:1,level:0,schema:'core',name:'Tenant',pkLoaded:false,fkLoaded:false},
        {id:2,level:1,schema:'party',name:'Company',pkLoaded:false,fkLoaded:false}
      ];
      window.edges = [{id:11,parentId:1,childId:2,name:'FK',columns:'Id → TenantId'}];
      window.events=[];
      const dotnet = {invokeMethodAsync: (name,...args) => {
        events.push([name,...args]);
        if (name==='RestoreGraph' || name==='ReplaceGraph') setTimeout(()=>graph.update(root,args[0],args[1],[1]),0);
        return Promise.resolve();
      }};
      graph.mount(root,initial,edges,dotnet,[1]);
    }''', script)
    node = lambda id: page.locator(f'.dbi-node[data-node-id="{id}"] .dbi-node-heading')
    node(1).click()
    assert page.evaluate("events.filter(e=>e[0]==='SelectTable').length") == 0, 'single click must not open inspector'
    assert page.locator('.dbi-node.selected').count() == 1
    node(1).dblclick()
    assert page.evaluate("events.filter(e=>e[0]==='SelectTable').length") >= 1, 'double click opens inspector'
    before_dismiss=page.evaluate("events.filter(e=>e[0]==='DismissDetails').length")
    node(2).click(modifiers=['Control'])
    assert page.locator('.dbi-node.selected').count()==2
    assert page.evaluate("events.filter(e=>e[0]==='DismissDetails').length") > before_dismiss
    page.evaluate('graph.focusBranch(root)')
    assert page.locator('.dbi-node.dimmed').count()==0, 'multiple selections must not focus'

    page.keyboard.down('Shift')
    assert page.locator('#root.graph-shift-held').count()==1
    page.wait_for_timeout(160)
    assert page.locator('.graph-shift-badge').first.evaluate('e=>getComputedStyle(e).opacity') == '1'
    page.keyboard.up('Shift')
    assert page.locator('#root.graph-shift-held').count()==0

    # Undo and redo must include SQL table expansion, not just camera operations.
    page.evaluate('''() => {
      const third={id:3,level:2,schema:'trade',name:'Order',pkLoaded:false,fkLoaded:false};
      graph.update(root,[{...initial[0],pkLoaded:true},initial[1],third],
        [...edges,{id:12,parentId:2,childId:3,name:'FK2',columns:'Id → CompanyId'}],[1]);
    }''')
    assert page.locator('.dbi-node').count()==3
    page.evaluate('graph.undo(root)')
    page.wait_for_timeout(30)
    assert page.locator('.dbi-node').count()==2
    assert page.evaluate("events.some(e=>e[0]==='RestoreGraph')")
    page.evaluate('graph.redo(root)')
    page.wait_for_timeout(30)
    assert page.locator('.dbi-node').count()==3

    # Shift-prune permanently removes unrelated topology; Reset remains layout-only.
    node(1).click()
    page.evaluate('graph.focusBranch(root,true)')
    page.wait_for_timeout(60)
    assert page.locator('.dbi-node').count()==2
    page.evaluate('graph.reset(root)')
    assert page.locator('.dbi-node').count()==2
    assert page.locator('.dbi-node.selected').count()==1
    page.evaluate('graph.undo(root)')
    assert page.locator('.dbi-node').count()==2
    page.evaluate('graph.undo(root)')
    assert page.locator('.dbi-node').count()==3

    # The shared PK/FK menu must position against its own specific button.
    page.evaluate('''() => {
      const menu=document.createElement('div');menu.className='dbi-fk-menu';
      menu.textContent='Select relations';root.querySelector('.graph-workspace').append(menu);
      graph.positionMenu(root,1,true);
      window.pkX=menu.style.left;
      graph.positionMenu(root,1,false);
      window.fkX=menu.style.left;
    }''')
    assert page.evaluate('pkX !== fkX'), 'PK picker must anchor under PK, not FK'
    assert not errors, errors
    print('PASS: click/double-click, Ctrl dismissal, focus guard, Shift badges, topology undo/redo, destructive prune and PK/FK anchors')
    browser.close()
