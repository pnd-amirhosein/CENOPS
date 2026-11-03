# Run: python tests/DatabaseInspectorGraphBrowser/test_graph.py
# Requires playwright and a local Chromium installation.
from pathlib import Path
from playwright.sync_api import sync_playwright
import json
root=Path(__file__).resolve().parents[2]
web=root/'src/NTSW_CENOPS.WebUI'
js=(web/'wwwroot/js/graph-interaction-support.js').read_text().replace('export function','function')+'\n'+(web/'wwwroot/js/graph-paths.js').read_text().replace('export function','function')+'\n'+(web/'wwwroot/js/graph-export.js').read_text().replace('export async function','async function').replace('export function','function')+'\n'+(web/'wwwroot/js/database-inspector-graph.js').read_text().replace("import { allRouteEdges, nearEdges } from './graph-paths.js';",'').replace("import { attachGraphModifierBadges, createGraphHistory } from './graph-interaction-support.js';",'').replace("import { attachGraphModifierBadges } from './graph-interaction-support.js';",'').replace("import { renderGraphBlob } from './graph-export.js';",'')
css='\n'.join([p.read_text() for p in [web/'wwwroot/css/database-inspector.css',web/'Components/Common/GraphWorkspace.razor.css']])
html='''<!doctype html><html><head><meta charset="utf-8"><style>body{margin:0}#root{width:1100px;height:720px}.app-notice-stack{padding:15px;background:white;border:1px solid #aac}.fake-toolbar{position:fixed;bottom:10px;right:10px;z-index:30}</style></head><body><div id="root" class="dbi-graph"><div class="graph-workspace"><div class="dbi-graph-stage graph-workspace-stage"></div><div class="graph-workspace-fullscreen-notices"><div class="app-notice-stack">تست اعلان در حالت تمام‌صفحه</div></div></div></div><div class="fake-toolbar"><button id="zoom">Zoom in</button><button id="more">Additional roots</button><button id="fullscreen">Fullscreen</button></div></body></html>'''
with sync_playwright() as p:
 browser=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
 page=browser.new_page(viewport={'width':1280,'height':900})
 errors=[];page.on('pageerror',lambda exc:errors.append(str(exc)))
 page.set_content(html)
 page.add_style_tag(content=css)
 page.add_style_tag(content='#root{height:720px!important;min-height:720px!important;flex:none!important;}')
 script='''async () => {
 const moduleText=REPLACEME;
 const url=URL.createObjectURL(new Blob([moduleText],{type:'text/javascript'}));
 const graph=await import(url);window.graph=graph;
 const root=document.getElementById('root');const nodes=[
 {id:1,schema:'core',name:'Tenant',level:0,pkLoaded:false,fkLoaded:false},
 {id:2,schema:'trade',name:'PurchaseOrder',level:0,pkLoaded:false,fkLoaded:false},
 {id:3,schema:'shipping',name:'Shipment',level:1,pkLoaded:false,fkLoaded:false},
 {id:4,schema:'customs',name:'Declaration',level:2,pkLoaded:false,fkLoaded:false}];
 const edges=[{id:11,childId:3,parentId:2},{id:12,childId:4,parentId:3}];
 window.actions=[]; const dotnet={invokeMethodAsync:(name,id)=>{window.actions.push({name,id});return Promise.resolve()}};
 graph.mount(root,nodes,edges,dotnet);
 document.getElementById('zoom').onclick=()=>graph.zoom(root,1.5);
 document.getElementById('more').onclick=()=>graph.update(root,[...nodes,{id:5,schema:'release',name:'GateEvent',level:0,pkLoaded:false,fkLoaded:false}],edges);
 document.getElementById('fullscreen').onclick=()=>graph.fullscreen(root);
 }'''.replace('REPLACEME',json.dumps(js))
 page.evaluate(script)
 assert page.locator('.dbi-node').count()==4
 assert page.locator('.dbi-relation-path').count()==2
 coords=page.locator('.dbi-node[data-node-id="1"], .dbi-node[data-node-id="2"]').evaluate_all('(nodes)=>nodes.map(n=>n.style.top)')
 assert len(set(coords))==2,coords
 page.locator('.dbi-node-action.pk').first.click()
 assert any(a['name']=='ExpandPrimary' for a in page.evaluate('window.actions'))
 page.locator('#zoom').click()
 assert page.locator('.dbi-canvas').evaluate('(e)=>e.style.zoom')=='1.5'
 assert 'scale(' not in page.locator('.dbi-pan').evaluate('(e)=>e.style.transform')
 assert round(page.locator('.dbi-node').first.bounding_box()['width'])==402
 page.locator('#more').click()
 assert page.locator('.dbi-node').count()==5
 coords=page.locator('.dbi-node[data-node-id="1"], .dbi-node[data-node-id="2"], .dbi-node[data-node-id="5"]').evaluate_all('(nodes)=>nodes.map(n=>n.style.top)')
 assert len(set(coords))==3,coords
 page.locator('#fullscreen').click()
 page.wait_for_timeout(150)
 isfull=page.evaluate('document.fullscreenElement === document.getElementById("root")')
 if isfull:
  assert page.locator('.graph-workspace-fullscreen-notices').evaluate('(e)=>getComputedStyle(e).display')=='block'
 print('Chromium: roots, edges, FK actions, crisp zoom, update, fullscreen host:',isfull,'errors:',errors)
 assert not errors
 browser.close()
