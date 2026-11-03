"""Glasswork 2.54: Backspace, Photoshop-style timeline, one-hop removal,
preview border, clear/undo, pixel-sharp zoom and graph toolbar affordances.
Run: python tests/DatabaseInspectorGraphBrowser/test_graph_254.py
"""
from pathlib import Path
from playwright.sync_api import sync_playwright

root_path = Path(__file__).resolve().parents[2]
web = root_path / 'src/NTSW_CENOPS.WebUI'
js = web / 'wwwroot/js'
def inline(name):
    text = '\n'.join(line for line in (js / name).read_text().splitlines() if not line.startswith('import {'))
    return text.replace('export async function','async function').replace('export function', 'function') if name != 'database-inspector-graph.js' else text
script = '\n'.join(map(inline, ['graph-interaction-support.js', 'graph-paths.js', 'graph-export.js', 'database-inspector-graph.js']))
html = '''<!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0">
<div id="root" class="dbi-graph" style="width:1260px;height:730px!important;min-height:0!important;position:relative">
<div class="graph-workspace"><div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div>
<div class="graph-workspace-controls"><button class="graph-clear-workspace">Clear</button>
<button><span class="graph-ctrl-badge">Ctrl</span>Undo</button>
<button class="graph-history-trigger">History</button>
<button><span class="graph-ctrl-badge">Ctrl</span>Redo</button></div>
</div></div><input id="editor" value="abc">
</body></html>'''
with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width': 1400, 'height': 850})
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.set_content(html)
    for file in [web/'wwwroot/css/database-inspector.css', web/'Components/Common/GraphWorkspace.razor.css']:
        page.add_style_tag(content=file.read_text())
    page.evaluate('''async source => {
      window.graph=await import(URL.createObjectURL(new Blob([source],{type:'text/javascript'})));
      window.root=document.getElementById('root');window.events=[];
      window.nodes=[{id:1,level:0,schema:'dbo',name:'A'},
        {id:2,level:0,schema:'dbo',name:'B'},
        {id:3,level:1,schema:'dbo',name:'C'},
        {id:4,level:2,schema:'dbo',name:'D'},
        {id:5,level:-1,schema:'dbo',name:'E'}];
      window.edges=[{id:11,parentId:1,childId:3},{id:12,parentId:3,childId:4},
                    {id:13,parentId:5,childId:3}];
      window.dotnet={invokeMethodAsync:(name,...args)=>{
        events.push([name,...args]);
        if (['ReplaceGraph','RestoreGraph'].includes(name))
          queueMicrotask(()=>graph.update(root,args[0],args[1],args[0].map(n=>n.id)));
        return Promise.resolve();
      }};
      graph.mount(root,nodes,edges,dotnet,[1,2]);
    }''', script)
    card = lambda id: page.locator(f'.dbi-node[data-node-id="{id}"]')
    remove = lambda id: card(id).locator('.dbi-node-remove')
    selected = lambda: {int(x) for x in page.locator('.dbi-node.selected').evaluate_all('els=>els.map(x=>x.dataset.nodeId)')}
    node_ids = lambda: {int(x) for x in page.locator('.dbi-node').evaluate_all('els=>els.map(x=>x.dataset.nodeId)')}

    # Marquee previews intersecting cards before pointer-up, and clears preview after.
    a,b = card(1).bounding_box(),card(2).bounding_box()
    page.keyboard.down('Control')
    page.mouse.move(a['x']-10,a['y']-10);page.mouse.down()
    page.mouse.move(a['x']+a['width']+10,a['y']+a['height']+10,steps=5)
    assert card(1).evaluate('e=>e.classList.contains("marquee-preview")')
    assert not card(2).evaluate('e=>e.classList.contains("marquee-preview")')
    page.mouse.move(b['x']+b['width']+10,b['y']+b['height']+10,steps=5)
    assert card(2).evaluate('e=>e.classList.contains("marquee-preview")')
    page.mouse.up();page.keyboard.up('Control')
    assert selected()=={1,2} and page.locator('.marquee-preview').count()==0

    # Backspace deletes both selected nodes and incident links; undo restores both.
    page.keyboard.press('Backspace')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===3')
    assert node_ids()=={3,4,5}
    assert page.evaluate('events.filter(e=>e[0]==="ReplaceGraph").at(-1)[2].length')==2
    assert page.evaluate('events.filter(e=>e[0]==="HistoryChanged").at(-1)[3].some(e=>e.label.includes("حذف 2"))')
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')
    assert selected()=={1,2}

    # Backspace on a form field must never mutate graph topology.
    page.locator('#editor').focus();page.keyboard.press('Backspace')
    assert node_ids()=={1,2,3,4,5}

    # Ctrl + minus removes only *one* PK layer: C is removed, D survives.
    card(1).hover();page.wait_for_timeout(160)
    page.keyboard.down('Control');page.wait_for_timeout(140)
    assert remove(1).locator('.dbi-remove-children').evaluate('e=>getComputedStyle(e).visibility')=='visible'
    remove(1).click(modifiers=['Control']);page.keyboard.up('Control')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===3')
    assert node_ids()=={2,4,5}
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')

    # Shift + minus removes direct FK parents (A and E), not transitive grandparent.
    card(3).hover();page.wait_for_timeout(160)
    page.keyboard.down('Shift');page.wait_for_timeout(140)
    badge=remove(3).locator('.dbi-remove-parents')
    assert badge.evaluate('e=>getComputedStyle(e).visibility')=='visible'
    assert badge.bounding_box()['x'] < remove(3).bounding_box()['x']
    remove(3).click(modifiers=['Shift']);page.keyboard.up('Shift')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===2')
    assert node_ids()=={2,4}
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')

    # History navigation jumps across several steps, and retains later entries.
    last=page.evaluate('events.filter(e=>e[0]==="HistoryChanged").at(-1)')
    steps,cursor=last[3],last[4]
    assert len(steps)>=3 and 0<=cursor<len(steps)
    page.evaluate('graph.goToHistory(root,0)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')
    assert page.evaluate('events.filter(e=>e[0]==="HistoryChanged").at(-1)[4]')==0
    assert page.evaluate('events.filter(e=>e[0]==="HistoryChanged").at(-1)[1]') is False
    page.evaluate('(i)=>graph.goToHistory(root,i)',cursor)
    assert node_ids()=={1,2,3,4,5}
    assert page.evaluate('events.filter(e=>e[0]==="HistoryChanged").at(-1)[4]')==cursor

    # Erasing all nodes must keep the history timeline and permit full undo.
    page.evaluate('graph.clearWorkspace(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===0')
    assert page.evaluate('events.filter(e=>e[0]==="HistoryChanged").at(-1)[3].at(-1).label')=='پاک‌سازی کل نمودار'
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')
    page.evaluate('graph.redo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===0')
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')

    # Zoom is layout-based, no GPU-scaled canvas image; rounded surfaces clip children.
    page.evaluate('graph.zoom(root,1.55)')
    actual=page.evaluate('''() => ({zoom:root.querySelector('.dbi-canvas').style.zoom,
      pan: getComputedStyle(root.querySelector('.dbi-pan')).transform,
      clip:getComputedStyle(root.querySelector('.dbi-node-surface')).overflow,
      radius:getComputedStyle(root.querySelector('.dbi-node-surface')).borderTopLeftRadius})''')
    assert float(actual['zoom'])>1 and actual['pan']=='none',actual
    assert actual['clip']=='hidden' and actual['radius']!='0px',actual
    page.keyboard.down('Control')
    badge = page.locator('.graph-ctrl-badge').first
    page.wait_for_timeout(160)
    assert badge.evaluate('e=>getComputedStyle(e).visibility')=='visible'
    assert badge.evaluate('e=>getComputedStyle(e).backgroundColor')=='rgb(255, 255, 255)'
    page.keyboard.up('Control')

    assert not errors,errors
    browser.close()
    print('PASS: live marquee, multiselect Backspace, one-hop removal/badges, history jumps, clear/undo/redo, sharp zoom and clipped corners')
