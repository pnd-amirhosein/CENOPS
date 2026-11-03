"""CENOPS 2.56: one-shot SQL draft storage contract (mock storage on blocked test origin)."""
from pathlib import Path
from playwright.sync_api import sync_playwright

repo=Path(__file__).resolve().parents[2]
script=(repo/'src/NTSW_CENOPS.WebUI/wwwroot/js/snippet-draft.js').read_text()
menu=(repo/'src/NTSW_CENOPS.WebUI/Components/Common/Menus/AppNestedMenu.razor').read_text()
assert '@onpointerenter=' not in menu and '@onfocus=' not in menu
assert '_expanded == item.Value ? null : item.Value' in menu

with sync_playwright() as pw:
    browser=pw.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page()
    # Navigation to test origins is restricted in this sandbox. Browser storage is
    # shimmed so the production draft helper can still be executed unchanged.
    page.set_content('<html><body>Test</body></html>')
    page.evaluate('''() => {
       const data = new Map();
       Object.defineProperty(window.crypto, 'randomUUID', {configurable: true, value: ()=>'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa'});
       Object.defineProperty(window, 'sessionStorage', {configurable:true, value:{
           setItem:(key,value)=>data.set(key,value),
           getItem:key=>data.get(key) ?? null,
           removeItem:key=>data.delete(key)
       }});
    }''')
    page.evaluate(script)
    token=page.evaluate('''() => cenopsSnippetDraft.store('sql', 'SELECT [a] FROM [dbo].[A];', 'sql', 'code')''')
    content=page.evaluate('''token => cenopsSnippetDraft.take(token)''',token)
    assert content['Kind']=='sql' and content['Content']=='SELECT [a] FROM [dbo].[A];',content
    assert page.evaluate('''token => cenopsSnippetDraft.take(token)''',token) is None
    browser.close()
print('PASS: click-only drawer contract and one-shot SQL draft storage behavior')
