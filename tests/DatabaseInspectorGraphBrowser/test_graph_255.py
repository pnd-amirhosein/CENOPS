"""Glasswork 2.55 — vector/raster export, bounds, logo and menu integration.
Run: python tests/DatabaseInspectorGraphBrowser/test_graph_255.py
"""
from pathlib import Path
from playwright.sync_api import sync_playwright

repo = Path(__file__).resolve().parents[2]
web = repo / 'src/NTSW_CENOPS.WebUI'
js = web / 'wwwroot/js'
parts = []
for name in ('graph-interaction-support.js','graph-paths.js','graph-export.js','database-inspector-graph.js'):
    source = '\n'.join(line for line in (js/name).read_text().splitlines() if not line.startswith('import {'))
    if name != 'database-inspector-graph.js':
        source = source.replace('export async function','async function').replace('export function','function')
    parts.append(source)
script = '\n'.join(parts) + '\nexport {buildGraphSvg,renderGraphBlob};'

html = '''<!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0">
<div class="skyline-logo"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 388 388"><rect width="388" height="388" rx="96" fill="#19B5DE"/><path d="M30 130h328v100H30z" fill="white"/></svg></div>
<div id="root" class="dbi-graph" style="width:1260px;height:730px!important;min-height:0!important;position:relative">
 <div class="graph-workspace"><div class="dbi-graph-stage graph-workspace-stage" tabindex="0"></div>
  <div class="graph-workspace-controls"><div class="app-nested-menu">
   <button class="app-nested-menu-trigger" aria-expanded="true">Share</button>
   <div class="app-nested-menu-popover drawer-open left-align"><div class="app-nested-menu-primary">
     <button class="app-nested-menu-item active">خروجی تصویر</button>
     <button class="app-nested-menu-item" disabled>خروجی کوئری</button></div>
     <div class="app-nested-menu-drawer"><div class="app-nested-menu-submenu">
       <button data-menu-value="image:png" class="app-nested-menu-item">PNG</button>
       <button data-menu-value="image:svg" class="app-nested-menu-item">SVG</button>
       <button data-menu-value="image:webp" class="app-nested-menu-item">WebP</button>
     </div></div></div>
  </div></div>
 </div>
</div></body></html>'''

# Exercise the shared CENOPS vector component used by both Skyline and About.
logo_source = (web/'Components/Common/CenopsMark.razor').read_text()
logo = logo_source[logo_source.index('<svg'):logo_source.index('</svg>') + len('</svg>')]
logo = logo.replace('class="@CssClass"', 'class="h-100 w-auto"')
html = html.replace('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 388 388"><rect width="388" height="388" rx="96" fill="#19B5DE"/><path d="M30 130h328v100H30z" fill="white"/></svg>',logo)

with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    context = browser.new_context(viewport={'width':1400,'height':850},accept_downloads=True)
    page = context.new_page()
    errors=[]
    page.on('pageerror',lambda e: errors.append(str(e)))
    page.set_content(html)
    for css in ('wwwroot/css/database-inspector.css','Components/Common/GraphWorkspace.razor.css','Components/Common/Menus/AppNestedMenu.razor.css'):
        page.add_style_tag(content=(web/css).read_text())
    page.evaluate('''async source => {
      window.graph=await import(URL.createObjectURL(new Blob([source],{type:'text/javascript'})));
      window.root=document.getElementById('root');window.events=[];
      window.nodes=[{id:1,level:0,schema:'dbo',name:'Declaration',pkLoaded:true},
        {id:2,level:1,schema:'finance',name:'BillOfLading',fkLoaded:true},
        {id:3,level:5,schema:'dbo',name:'A&B <Invoice>',pkLoaded:false}];
      window.edges=[{id:11,parentId:1,childId:2},{id:12,parentId:2,childId:3}];
      window.dotnet={invokeMethodAsync:(name,...args)=>{events.push([name,...args]);return Promise.resolve();}};
      graph.mount(root,nodes,edges,dotnet,[1]);
      // Export needs to ignore the current viewport size and pan state.
      root.querySelectorAll('.dbi-node')[2].dataset.test='offscreen';
    }''',script)
    # Pure SVG validation: full node bounds, all edges, escaped SQL names,
    # rounded frame, dotted grid, correct version and watermark artwork.
    svg = page.evaluate('''() => graph.buildGraphSvg({
      nodes:[{id:1,schema:'dbo',name:'A&B <Invoice>',x:-500,y:-240},
        {id:2,schema:'finance',name:'Bill',x:1500,y:1400}],
      edges:[{d:'M -400 -200 C 50 100 800 500 1500 1500',color:'#0897b5'}],
      branding:{name:'گمرک · سیناپس',version:'CENOPS · GLASSWORK 2.55'},
      logo:document.querySelector('.skyline-logo svg').outerHTML}).svg''')
    assert '&amp;B &lt;Invoice&gt;' in svg and '<pattern' in svg and 'GLASSWORK 2.55' in svg
    assert '<clipPath' in svg and 'rx="18"' in svg and 'M -400 -200' in svg
    assert 'text-anchor="start" direction="rtl"' in svg
    assert '#19B5DE' in svg  # Shared CENOPS vector mark; its viewport clips the rounded rectangle.
    assert page.evaluate('''value => !new DOMParser().parseFromString(value,'image/svg+xml').querySelector('parsererror')''',svg)
    assert page.evaluate('''async () => {
      const b=await graph.renderGraphBlob({nodes:[{id:1,x:0,y:0,name:'A',schema:'dbo'}]},'png');
      return b.type==='image/png' && b.size>1500;
    }''')
    assert page.evaluate('''async () => {
      const b=await graph.renderGraphBlob({nodes:[{id:1,x:0,y:0,name:'A',schema:'dbo'}]},'webp');
      return b.type==='image/webp' && b.size>500;
    }''')
    # The menu expands alongside its primary column and does not cover the choice.
    assert page.locator('.app-nested-menu-popover').evaluate('e=>getComputedStyle(e).display') == 'flex'
    assert page.locator('.app-nested-menu-drawer').bounding_box()['width'] > 110
    assert page.locator('[data-menu-value="image:svg"]').is_visible()
    assert page.locator('button:has-text("خروجی کوئری")').is_disabled()
    page.evaluate('graph.zoom(root,1.8)')
    # Real native click must open an image in a separate tab, even though
    # generated SVG is delivered asynchronously after the immediate popup.
    for format in ('svg','png','webp'):
        with context.expect_page() as tab_info:
            page.locator(f'[data-menu-value="image:{format}"]').click()
        tab=tab_info.value
        page.wait_for_timeout(250)
        for attempt in range(30):
            if tab.url.startswith('blob:'): break
            page.wait_for_timeout(100)
        assert tab.url.startswith('blob:'), (format,tab.url,errors)
        assert tab.url.startswith('blob:')
        if format == 'svg':
            exported = page.evaluate('url=>fetch(url).then(r=>r.text())',tab.url)
            assert 'A&amp;B &lt;Invoice&gt;' in exported and 'GLASSWORK 2.67' in exported
            assert '#19B5DE' in exported and 'cenops-export-dots' in exported
            assert page.evaluate('value=>!new DOMParser().parseFromString(value,"image/svg+xml").querySelector("parsererror")',exported)
        else:
            signature = page.evaluate('''async url=>{
              const bytes=new Uint8Array(await (await fetch(url)).arrayBuffer());
              return [...bytes.slice(0,12)];
            }''',tab.url)
            assert signature[:4] == ([137,80,78,71] if format=='png' else [82,73,70,70])
        tab.close()
    assert not errors, errors
    context.close();browser.close()
print('Glasswork 2.55 graph export regression passed.')
