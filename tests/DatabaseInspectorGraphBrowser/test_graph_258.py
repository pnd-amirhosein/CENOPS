"""GLASSWORK 2.58: graph edge insertion, zero-count export and shared Save As icons.
Run: python tests/DatabaseInspectorGraphBrowser/test_graph_258.py
"""
from pathlib import Path
import ast
import re
from xml.etree import ElementTree
from playwright.sync_api import sync_playwright

repo = Path(__file__).resolve().parents[2]
web = repo / 'src/NTSW_CENOPS.WebUI'
source = (web / 'Components/Pages/DatabaseInspectorPage.razor').read_text()
view = (web / 'Components/Common/DatabaseGraphView.razor').read_text()
shell = (web / 'Components/Common/GraphWorkspace.razor').read_text()
nav = (web / 'Components/Layout/NavMenu.razor').read_text()
save = (web / 'Components/Pages/CodeCleanupPage.razor').read_text()
artwork = (web / 'Components/SideBar/KnowledgeMenuIcons.cs').read_text()

assert 'انتخاب جدول الزامی است' not in view
assert 'Disabled: _selectedIds.Count == 0, Count: _selectedIds.Count' in view
assert 'new("query", "خروجی کوئری", Disabled: true, Count: 0)' in shell
for key in ('Curl','Sql','Note'):
    assert f'KnowledgeMenuIcons.{key}' in nav and f'KnowledgeMenuIcons.{key}' in save
    value = re.search(r'public const string ' + key + r' = ("(?:\\.|[^"\\])*");', artwork)
    assert value is not None, key
    svg = bytes(value.group(1)[1:-1], 'utf-8').decode('unicode_escape')
    assert ElementTree.fromstring(svg).tag == 'svg', key
assert '@((MarkupString)choice.Icon)' in save
assert 'code-tool-choice-icon svg' in (web / 'wwwroot/css/snippet-tools.css').read_text()

# Inspect the actual server-side add paths: both dropdown insertion and FK/PK
# expansion must load the inserted tables' catalog and reconcile their edges.
assert 'UpdateFocusedGraph(desired, available, relations)' in source
assert 'ConnectInsertedNodes(inserted, insertedRelations)' in source
assert 'ConnectInsertedNodes(inserted, insertedCatalog)' in source
assert 'ConnectInsertedNodes(inserted, catalog)' in source
assert 'GetRelationsAsync(actor, _source, id)' in source
assert 'GetAllRelationsAsync(actor, _source)' in source
assert 'if (!inserted.Contains(fk.Child.Id) && !inserted.Contains(fk.Parent.Id)) continue;' in source
assert 'if (!_vertices.ContainsKey(fk.Child.Id) || !_vertices.ContainsKey(fk.Parent.Id)) continue;' in source
assert 'RemoveDanglingRelations();\n        ConnectInsertedNodes(inserted, insertedRelations);' in source

js_dir = web / 'wwwroot/js'
parts = []
for name in ('graph-interaction-support.js', 'graph-paths.js', 'graph-export.js', 'database-inspector-graph.js'):
    code = '\n'.join(line for line in (js_dir / name).read_text().splitlines() if not line.startswith('import {'))
    if name != 'database-inspector-graph.js':
        code = code.replace('export async function','async function').replace('export function','function')
    parts.append(code)
script = '\n'.join(parts)

with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width':1350,'height':860})
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.set_content('''<!doctype html><html><body style="margin:0"><div id="root" class="dbi-graph"
        style="width:1250px;height:740px!important;min-height:0!important;position:relative">
        <div class="graph-workspace"><div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div></div>
        </div></body></html>''')
    for css in ('wwwroot/css/database-inspector.css','Components/Common/GraphWorkspace.razor.css'):
        page.add_style_tag(content=(web/css).read_text())
    page.evaluate('''async source => {
        window.graph=await import(URL.createObjectURL(new Blob([source],{type:'text/javascript'})));
        window.root=document.getElementById('root');
        window.dotnet={invokeMethodAsync:()=>Promise.resolve()};
        window.old={id:1,schema:'dbo',name:'Existing',level:0};
        window.newNode={id:2,schema:'dbo',name:'Incoming',level:1};
        window.unrelated={id:3,schema:'dbo',name:'Other',level:0};
        window.fk={id:501,parentId:1,childId:2,name:'FK_Incoming_Existing',columns:'ExistingId → Id'};
        window.fk2={id:502,parentId:3,childId:2,name:'FK_Incoming_Other',columns:'OtherId → Id'};
        graph.mount(root,[old,unrelated],[],dotnet,[1,3]);
    }''',script)
    assert page.locator('.dbi-node').count() == 2
    assert page.locator('.dbi-relation-path').count() == 0
    page.evaluate('graph.update(root,[old,unrelated,newNode],[fk,fk2],[1,3,2])')
    assert page.locator('.dbi-node').count() == 3
    assert page.locator('.dbi-relation-path').count() == 2
    # Remove the new vertex (simulating a picker removal), then re-add it with
    # the same FK metadata; neither a graph remount nor a layout reset is needed.
    page.evaluate('graph.update(root,[old,unrelated],[],[1,3])')
    assert page.locator('.dbi-relation-path').count() == 0
    page.evaluate('graph.update(root,[old,unrelated,newNode],[fk,fk2],[1,2,3])')
    assert page.locator('.dbi-relation-path').count() == 2
    assert not errors, errors
    browser.close()
print('GLASSWORK 2.58: connection rendering, zero badge, shared SVG icons and server reconciliation wiring passed.')
