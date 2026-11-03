"""New shortcut + global overlay regression: python tests/DatabaseInspectorGraphBrowser/test_graph_257.py"""
from pathlib import Path
from playwright.sync_api import sync_playwright

repo = Path(__file__).resolve().parents[2]
web = repo / 'src/NTSW_CENOPS.WebUI'
js_dir = web / 'wwwroot/js'
parts = []
for name in ('graph-interaction-support.js','graph-paths.js','graph-export.js','database-inspector-graph.js'):
    source = '\n'.join(line for line in (js_dir/name).read_text().splitlines() if not line.startswith('import {'))
    if name != 'database-inspector-graph.js':
        source = source.replace('export async function','async function').replace('export function','function')
    parts.append(source)
source = '\n'.join(parts)
with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page = browser.new_page(viewport={'width':1400,'height':850})
    errors=[]
    page.on('pageerror',lambda e: errors.append(str(e)))
    page.set_content('''<!doctype html><html><body style="margin:0">
        <div class="app-modal-backdrop" id="dialog" style="display:none"><section class="app-modal-surface">SQL query</section></div>
        <div class="app-shell" style="height:850px"><header class="shell-topbar" id="skyline">Skyline</header>
        <aside class="shell-sidebar" id="side">Sidebar</aside><div id="root" class="dbi-graph"
        style="width:1050px;height:650px;position:relative"><div class="graph-workspace">
        <div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div></div></div></div>
        <input id="editor" value="foo"></body></html>''')
    for css in ('wwwroot/css/operations-shell.css','wwwroot/css/database-inspector.css',
                'Components/Common/GraphWorkspace.razor.css','Components/Common/AppModal.razor.css'):
        page.add_style_tag(content=(web/css).read_text())
    page.evaluate('''async source => {
        window.graph=await import(URL.createObjectURL(new Blob([source],{type:'text/javascript'})));
        window.root=document.getElementById('root');window.events=[];
        window.nodes=[1,2,3,4].map(id=>({id,level:id-1,schema:'dbo',name:'Table'+id}));
        window.dotnet={invokeMethodAsync:(name,...args)=>{events.push([name,...args]);return Promise.resolve();}};
        graph.mount(root,nodes,[],dotnet,[1]);
    }''', source)
    page.locator('.dbi-graph-stage').focus()
    page.keyboard.press('Control+a')
    assert page.locator('.dbi-node.selected').count()==4
    assert page.evaluate('events.filter(x=>x[0]==="SelectionChanged").at(-1)[1]')==[1,2,3,4]
    page.keyboard.press('Control+a')
    assert page.locator('.dbi-node.selected').count()==4
    page.locator('#editor').focus()
    page.keyboard.press('Control+a')
    assert page.locator('.dbi-node.selected').count()==4
    # Must not capture Ctrl+A in a normal editor (only the editor's text is highlighted).
    assert page.evaluate('''() => {const input=document.getElementById("editor");return input.selectionStart===0 && input.selectionEnd===3;}''')
    page.evaluate('document.getElementById("dialog").style.display="flex"')
    modal=page.locator('.app-modal-surface').bounding_box()
    center=(modal['x']+modal['width']/2, modal['y']+modal['height']/2)
    hit=page.evaluate('''point => document.elementFromPoint(point[0],point[1])?.closest('#dialog')?.id''',center)
    assert hit=='dialog',f'Modal should be in front of shell: {hit}'
    assert not errors,errors
    browser.close()
print('GLASSWORK 2.57: Ctrl+A and application overlay checks passed.')
