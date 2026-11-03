"""2.53: box selection, directed removal, live topology synchronization, Shift-prune and undo.
Run: python tests/DatabaseInspectorGraphBrowser/test_graph_253.py
"""
from pathlib import Path
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
web = root / 'src/NTSW_CENOPS.WebUI'
js = web / 'wwwroot/js'
def inline(name):
    return '\n'.join(line for line in (js/name).read_text().splitlines()
                     if not line.startswith('import {')).replace('export function', 'function')
script = '\n'.join([inline('graph-interaction-support.js'), inline('graph-paths.js'), inline('graph-export.js').replace('export async function','async function'),
                    '\n'.join(line for line in (js/'database-inspector-graph.js').read_text().splitlines()
                              if not line.startswith('import {'))])
html = '''<!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0">
<div id="root" class="dbi-graph" style="width:1280px;height:750px!important;min-height:0!important;position:relative">
<div class="graph-workspace"><div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div></div></div>
</body></html>'''
with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width': 1350, 'height': 850})
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.set_content(html)
    page.add_style_tag(content=(web/'wwwroot/css/database-inspector.css').read_text())
    page.add_style_tag(content=(web/'Components/Common/GraphWorkspace.razor.css').read_text())
    page.evaluate('''async source => {
      window.graph=await import(URL.createObjectURL(new Blob([source],{type:'text/javascript'})));
      window.root=document.querySelector('#root');window.events=[];
      window.nodes=[
        {id:1,level:0,schema:'dbo',name:'First'},
        {id:2,level:0,schema:'dbo',name:'Second'},
        {id:3,level:1,schema:'dbo',name:'Third'},
        {id:4,level:1,schema:'dbo',name:'Fourth'},
        {id:5,level:-1,schema:'dbo',name:'Fifth'},
        {id:6,level:2,schema:'dbo',name:'Sixth'}];
      window.edges=[
        {id:11,parentId:1,childId:3},
        {id:12,parentId:3,childId:6},
        {id:13,parentId:5,childId:3},
        {id:14,parentId:5,childId:4},
        {id:15,parentId:6,childId:1}]; // Cycle between First, Third, Sixth.
      window.dotnet={invokeMethodAsync:(name,...args)=>{
        events.push([name,...args]);
        if (['ReplaceGraph','RestoreGraph'].includes(name))
          queueMicrotask(()=>graph.update(root,args[0],args[1],[]));
        return Promise.resolve();
      }};
      graph.mount(root,nodes,edges,dotnet,[]);
    }''', script)
    card = lambda id: page.locator(f'.dbi-node[data-node-id="{id}"]')
    action = lambda id: page.locator(f'.dbi-node[data-node-id="{id}"] .dbi-node-remove')

    # Ctrl + drag on blank stage crosses the two root-level cards and no other nodes.
    box1, box2 = card(1).bounding_box(), card(2).bounding_box()
    assert box1['x'] == box2['x'] and box1['y'] < box2['y'], (box1,box2)
    x1, y1 = box1['x']-9, box1['y']-9
    x2, y2 = box2['x']+box2['width']+9, box2['y']+box2['height']+9
    page.keyboard.down('Control')
    page.mouse.move(x1,y1)
    page.mouse.down()
    page.mouse.move(x2,y2,steps=8)
    assert page.locator('.dbi-marquee.active').count()==1
    page.mouse.up()
    page.keyboard.up('Control')
    assert page.locator('.dbi-node.selected').count()==2, 'marquee selects both roots'
    assert page.evaluate('events.filter(e=>e[0]==="SelectionChanged").at(-1)[1].sort().join()')=='1,2'
    assert page.locator('.dbi-marquee.active').count()==0

    # Ordinary hover minus removes exactly one node and incident links; undo restores.
    card(4).hover()
    page.wait_for_timeout(180)
    assert action(4).evaluate('e=>getComputedStyle(e).visibility')=='visible'
    action(4).click()
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===5')
    assert card(4).count()==0
    assert page.evaluate('events.filter(e=>e[0]==="ReplaceGraph").at(-1)[2].every(e=>e.parentId!==4&&e.childId!==4)')
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===6')

    # Ctrl removes only immediate PK children. Cycle/second-hop nodes survive.
    card(1).hover()
    page.keyboard.down('Control')
    page.wait_for_timeout(100)
    assert action(1).locator('.dbi-remove-children').evaluate('e=>getComputedStyle(e).visibility')=='visible'
    action(1).click(modifiers=['Control'])
    page.keyboard.up('Control')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===4')
    assert {int(x) for x in page.locator('.dbi-node').evaluate_all('els=>els.map(e=>e.dataset.nodeId)')} == {2,4,5,6}
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===6')

    # Shift removes only immediate FK parents of Third (First and Fifth, NOT Sixth).
    card(3).hover()
    page.keyboard.down('Shift')
    page.wait_for_timeout(100)
    assert action(3).locator('.dbi-remove-parents').evaluate('e=>getComputedStyle(e).visibility')=='visible'
    action(3).click(modifiers=['Shift'])
    page.keyboard.up('Shift')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===3')
    assert {int(x) for x in page.locator('.dbi-node').evaluate_all('els=>els.map(e=>e.dataset.nodeId)')} == {2,4,6}
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===6')

    # A Shift path operation sends a reduced *topology* to Blazor (no hidden set).
    card(2).locator('.dbi-node-heading').click()
    page.evaluate('graph.focusBranch(root,true)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===1')
    assert card(2).count()==1
    assert page.evaluate('events.filter(e=>e[0]==="ReplaceGraph").at(-1)[1].map(x=>x.id).join()')=='2'
    # Layout reset cannot resurrect previously pruned nodes.
    page.evaluate('graph.reset(root)')
    assert page.locator('.dbi-node').count()==1
    page.evaluate('graph.undo(root)') # Undo layout reset
    page.evaluate('graph.undo(root)') # Undo pruning
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===6')

    # Shift path between two nodes retains ONLY the route; non-route edges vanish too.
    card(1).locator('.dbi-node-heading').click()
    card(5).locator('.dbi-node-heading').click(modifiers=['Control'])
    assert page.evaluate('graph.highlightPath(root,true)') is True
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===4')
    assert {int(x) for x in page.locator('.dbi-node').evaluate_all('els=>els.map(e=>e.dataset.nodeId)')} == {1,3,5,6}
    assert page.locator('.dbi-relation-path').count() == 4
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===6')
    # Even when the final node is removed, the mounted workspace keeps Undo usable.
    page.evaluate('graph.update(root,[{id:1,level:0,schema:"dbo",name:"First"}],[],[])')
    assert page.locator('.dbi-node').count() == 1
    card(1).hover()
    page.wait_for_timeout(180)
    action(1).click()
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===0')
    page.evaluate('graph.undo(root)')
    page.wait_for_function('document.querySelectorAll(".dbi-node").length===1')

    assert not errors, errors
    browser.close()
    print('PASS: box selection, hover minus, immediate PK children and FK parents, cyclic graphs, destructive focus, undo/reset')
