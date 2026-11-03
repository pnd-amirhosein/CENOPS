"""Run: python tests/GraphInteractionBrowser/test_interactions.py
Browser smoke test with the real ES modules, no database or .NET server needed.
"""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
web='src/NTSW_CENOPS.WebUI/'
html = f'''<!doctype html><html><head><meta charset="utf-8">
<style>body{{margin:0}} .dbi-graph,.source-graph{{position:relative!important;display:block;width:1900px;height:680px;overflow:hidden;}}
#root{{margin-bottom:30px}} #src .source-graph-stage{{position:absolute;inset:0;}}</style></head><body>
<div id="root" class="dbi-graph"><div class="graph-workspace"><div class="dbi-graph-stage graph-workspace-stage"></div></div></div>
<div id="src" class="source-graph"><div class="graph-workspace"><div class="source-graph-stage graph-workspace-stage"><svg class="source-graph-svg"><g class="source-graph-viewport"></g></svg><div class="source-graph-tooltip"></div></div></div></div></body></html>'''
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path='/usr/bin/chromium', headless=True,args=['--no-sandbox'])
    page = browser.new_page(viewport={'width':2000,'height':1500})
    errors=[]; page.on('pageerror', lambda exc: errors.append(str(exc)))
    page.set_content(html)
    page.add_style_tag(content='\n'.join((root / part).read_text() for part in [
        f'{web}wwwroot/css/database-inspector.css',
        f'{web}Components/Common/GraphWorkspace.razor.css',
        f'{web}Components/Common/SourceGraphView.razor.css']))
    page.add_style_tag(content='#root,#src{height:680px!important;min-height:680px!important;flex:none!important;}')
    helper=(root/f'{web}wwwroot/js/graph-interaction-support.js').read_text().replace('export function','function')+'\n'+(root/f'{web}wwwroot/js/graph-paths.js').read_text().replace('export function','function')
    helper+='\n'+(root/f'{web}wwwroot/js/graph-export.js').read_text().replace('export async function','async function').replace('export function','function')
    scripts={name: helper+'\n'+(root/f'{web}wwwroot/js/{name}-graph.js').read_text().replace("import { allRouteEdges, nearEdges } from './graph-paths.js';",'').replace("import { attachGraphModifierBadges, createGraphHistory } from './graph-interaction-support.js';",'').replace("import { attachGraphModifierBadges } from './graph-interaction-support.js';",'').replace("import { renderGraphBlob } from './graph-export.js';",'') for name in ['database-inspector']}
    scripts['source'] = helper+'\n'+(root/f'{web}wwwroot/js/source-relation-graph.js').read_text().replace("import { allRouteEdges, nearEdges } from './graph-paths.js';",'').replace("import { attachGraphModifierBadges, createGraphHistory } from './graph-interaction-support.js';",'').replace("import { attachGraphModifierBadges } from './graph-interaction-support.js';",'')
    scripts['paths'] = (root/f'{web}wwwroot/js/graph-paths.js').read_text()

    page.evaluate(f'''async () => {{
      const asModule=async text=>import(URL.createObjectURL(new Blob([text],{{type:'text/javascript'}})));
      window.db=await asModule({json.dumps(scripts['database-inspector'])});
      window.src=await asModule({json.dumps(scripts['source'])});
      window.path=await asModule({json.dumps(scripts['paths'])});
      window.called=[];
      window.dotnet={{invokeMethodAsync:(name,...args)=>{{called.push([name,...args]);if(name==='ReplaceGraph'||name==='RestoreGraph')queueMicrotask(()=>db.update(document.getElementById('root'),args[0],args[1],[]));return Promise.resolve();}}}};
      const nodes=[1,2,3,4,5].map((id,i)=>({{id,schema:'test',name:'Table'+id,level:i===0?0:i}}));
      const edges=[{{id:11,parentId:1,childId:2}},{{id:12,parentId:2,childId:3}},{{id:13,parentId:1,childId:3}},{{id:14,parentId:3,childId:4}}];
      db.mount(document.getElementById('root'),nodes,edges,dotnet,[]);
      const snodes=[1,2,3,4,5].map((id,i)=>({{id:'s'+id,title:'Method'+id,subtitle:'Owner',kind:'Method',level:i}}));
      const sedges=[{{from:'s1',to:'s2'}},{{from:'s2',to:'s3'}},{{from:'s1',to:'s3'}},{{from:'s3',to:'s4'}}];
      src.mount(document.getElementById('src'),snodes,sedges,dotnet);
    }}''')
    root=page.locator('#root');sroot=page.locator('#src')
    assert root.locator('.dbi-node').count()==5
    assert root.locator('.dbi-relation-path').count()==4
    assert root.locator('.dbi-relation-path').evaluate_all('(xs)=>new Set(xs.map(x=>x.style.stroke)).size')>1
    # Ctrl+click toggles and preserves another node; plain click resets to one.
    root.locator('.dbi-node[data-node-id="1"] .dbi-node-heading').click()
    root.locator('.dbi-node[data-node-id="4"] .dbi-node-heading').click(modifiers=['Control'])
    selected=root.locator('.dbi-node.selected').count();assert selected==2,selected
    before=root.locator('.dbi-pan').evaluate('(el)=>el.style.transform');zoom=root.locator('.dbi-canvas').evaluate('(el)=>el.style.zoom')
    assert page.evaluate('db.focusBranch(document.getElementById("root"))') is None
    assert before==root.locator('.dbi-pan').evaluate('(el)=>el.style.transform')
    assert zoom==root.locator('.dbi-canvas').evaluate('(el)=>el.style.zoom')
    # Focus requires exactly one selected node, so multi-select must leave the view unchanged.
    assert root.locator('.dbi-node.dimmed').count()==0
    assert page.evaluate('db.highlightPath(document.getElementById("root"))') is True
    assert before==root.locator('.dbi-pan').evaluate('(el)=>el.style.transform')
    assert zoom==root.locator('.dbi-canvas').evaluate('(el)=>el.style.zoom')
    assert root.locator('.dbi-relation-path.highlighted').count()==4 # two valid alternative paths
    assert page.evaluate('db.highlightPath(document.getElementById("root"),true)') is True
    page.wait_for_function('document.querySelectorAll("#root .dbi-node").length === 4')
    # Reset cannot restore permanently pruned graph topology.
    page.evaluate('db.reset(document.getElementById("root"))')
    assert root.locator('.dbi-node').count()==4
    page.evaluate('db.undo(document.getElementById("root"))') # Restore pre-tidy view.
    page.evaluate('db.undo(document.getElementById("root"))') # Undo isolation.
    page.wait_for_function('document.querySelectorAll("#root .dbi-node").length === 5')
    # The previous two-node selection is restored after undoing isolation.
    assert root.locator('.dbi-node.selected').count()==2
    # Shift+focus prunes to the selected node and its immediate neighbors.
    root.locator('.dbi-node[data-node-id="3"] .dbi-node-heading').click()
    page.evaluate('db.focusBranch(document.getElementById("root"),true)')
    page.wait_for_function('document.querySelectorAll("#root .dbi-node").length === 4')
    assert root.locator('.dbi-node').count()==4 # 1, 2, 3, 4; not disconnected 5
    assert root.locator('.dbi-relation-path').count()==3
    page.evaluate('db.undo(document.getElementById("root"))') # Undo focus prune.
    page.wait_for_function('document.querySelectorAll("#root .dbi-node").length === 5')
    # Disconnected pair has no route and must leave the previous camera unchanged.
    root.locator('.dbi-node[data-node-id="1"] .dbi-node-heading').click()
    root.locator('.dbi-node[data-node-id="5"] .dbi-node-heading').click(modifiers=['Control'])
    camera=root.locator('.dbi-pan').evaluate('(e)=>e.style.transform')
    assert page.evaluate('db.highlightPath(document.getElementById("root"))') is False
    assert root.locator('.dbi-pan').evaluate('(e)=>e.style.transform')==camera
    page.evaluate('db.reset(document.getElementById("root"))')
    # Plain selection followed by blank canvas click dismisses details.
    root.locator('.dbi-node[data-node-id="2"] .dbi-node-heading').click()
    root.locator('.dbi-graph-stage').click(position={'x':1070,'y':630})
    assert ['DismissDetails'] in page.evaluate('called')
    assert root.locator('.dbi-node.selected').count()==0
    # Source component: multi-select, all-route highlight, then Shift isolate.
    sroot.locator('[data-node-id="s1"]').click()
    sroot.locator('[data-node-id="s2"]').click(modifiers=['Control'])
    assert sroot.locator('.graph-vertex.is-selected').count()==2
    initial=sroot.locator('.source-graph-viewport').get_attribute('transform')
    assert page.evaluate('src.highlightPath(document.getElementById("src"))') is True
    assert sroot.locator('.source-graph-viewport').get_attribute('transform')==initial
    page.evaluate('src.focusBranch(document.getElementById("src"))')
    assert sroot.locator('.source-graph-viewport').get_attribute('transform')==initial
    assert page.evaluate('src.highlightPath(document.getElementById("src"),true)') is True
    assert sroot.locator('.graph-vertex').count()==2
    # Source clicking blank canvas dismisses metadata.
    sroot.locator('.source-graph-stage').click(position={'x':1040,'y':640})
    assert page.evaluate('called.filter(x=>x[0]==="DismissDetails").length')>=2
    # Parallel edges: all distinct FK keys retained. Disconnected: no path.
    result=page.evaluate('''() => {
        const edges=[{id:1,from:1,to:2},{id:2,from:1,to:2},{id:3,from:2,to:3}];
        const found=path.allRouteEdges([1,2,3,4],edges,1,3);
        return {edges:[...found.edges].sort(),disconnected:path.allRouteEdges([1,2,3,4],edges,1,4)};
    }''')
    assert result=={'edges':[1,2,3],'disconnected':None},result
    assert not errors, errors
    print('PASS: DB and Source; Ctrl selection, 2-node paths, all alternative routes, camera preservation, destructive Shift pruning, dismiss, colors, cycles/parallel links')
    browser.close()

