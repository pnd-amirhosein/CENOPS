"""C.E.N.O.P.S. 2.66 — About dialog layout and public identity regression.

Run from anywhere: python tests/AboutDialogBrowser/test_about_261.py
This verifies the actual Razor body and component stylesheet without needing a
running SQL Server or replacing Blazor's integration tests.
"""
from pathlib import Path
import base64
import re
from playwright.sync_api import sync_playwright

repo = Path(__file__).resolve().parents[2]
web = repo / 'src/NTSW_CENOPS.WebUI'
components = web / 'Components/Common'
about = (components / 'CenopsAboutDialog.razor').read_text(encoding='utf-8')
style = (components / 'CenopsAboutDialog.razor.css').read_text(encoding='utf-8')
release = (components / 'CenopsRelease.cs').read_text(encoding='utf-8')
modal = (components / 'AppModal.razor').read_text(encoding='utf-8')

assert 'Title="درباره سامانه"' not in about
assert 'AccessibleLabel="درباره گمرک / سیناپس"' in about
assert 'public string? AccessibleLabel { get; set; }' in modal
assert 'aria-label="@AccessibleLabelWhenNoTitle"' in modal
assert 'public const string Product = "C.E.N.O.P.S."' in release
assert 'public const string Version = "2.67"' in release
assert 'public const string Codename = "GLASSWORK"' in release
assert '<CenopsMark CssClass="cenops-about-mark" />' in about
assert 'FlushContent="true"' in about
assert 'public bool FlushContent { get; set; }' in modal

# Render the unmodified declarative body with release constants substituted;
# only Blazor's component invocation and Razor expressions need expansion.
body = about.split('<ChildContent>', 1)[1].split('</ChildContent>', 1)[0]
svg_source = (components / 'CenopsMark.razor').read_text(encoding='utf-8')
svg = svg_source[svg_source.index('<svg'):svg_source.index('</svg>') + 6]
svg = svg.replace('class="@CssClass"', 'class="cenops-about-mark"')
body = body.replace('<CenopsMark CssClass="cenops-about-mark" />', svg)
constants = dict(re.findall(r'public const string (\w+) = "([^"]+)";', release))
for key, value in constants.items():
    body = body.replace(f'@CenopsRelease.{key}', value)
assert '@CenopsRelease' not in body
mark = base64.b64encode((web / 'wwwroot/img/ilecy-mark.png').read_bytes()).decode('ascii')
body = body.replace('src="img/ilecy-mark.png"', f'src="data:image/png;base64,{mark}"')
style = style.replace('::deep ', '')  # Razor CSS isolation compiler's selector expansion.
modal_css = (components / 'AppModal.razor.css').read_text(encoding='utf-8')
html = f'''<!doctype html><html lang="fa" dir="rtl"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>* {{ box-sizing: border-box; }}
body {{ margin:0; background:#dbe4e9; font-family:Tahoma,Arial,sans-serif; }}
{modal_css}
{style}</style>
<div class="app-modal-backdrop">
<section class="app-modal-surface cenops-about-surface app-modal-surface--flush" style="--app-modal-width:56rem"
  role="dialog" aria-modal="true" aria-label="درباره گمرک / سیناپس">
<div class="app-modal-body">{body}</div>
</section></div></html>'''

with sync_playwright() as playwright:
    browser = playwright.chromium.launch(headless=True, executable_path='/usr/bin/chromium', args=['--no-sandbox'])
    for width, height in [(1280, 850), (860, 750), (390, 844), (360, 568)]:
        page = browser.new_page(viewport={'width': width, 'height': height})
        page.set_content(html)
        page.wait_for_timeout(180)  # Dialog entrance animation finishes.
        assert page.locator('[role="dialog"]').get_attribute('aria-label') == 'درباره گمرک / سیناپس'
        assert page.get_by_text('C.E.N.O.P.S.', exact=True).count() >= 1
        assert page.get_by_text('GLASSWORK', exact=True).count() == 1
        assert page.locator('.cenops-about-legal').is_visible()
        assert page.locator('.cenops-about-powered img').is_visible()
        assert page.locator('.cenops-about-powered strong').evaluate('e=>getComputedStyle(e).fontSize') == '16px'
        assert page.locator('.cenops-about-powered > span').evaluate('e=>getComputedStyle(e).color') == 'rgb(0, 102, 255)'
        assert page.locator('.cenops-about-powered strong').evaluate('e=>getComputedStyle(e).color') == 'rgb(0, 102, 255)'
        surface = page.locator('.app-modal-surface')
        assert surface.evaluate('e => getComputedStyle(e).padding') == '0px'
        assert surface.evaluate('e => getComputedStyle(e).overflow') == 'hidden'
        brand_el = page.locator('.cenops-about-brand')
        brand = brand_el.bounding_box()
        details = page.locator('.cenops-about-details').bounding_box()
        assert brand and details
        assert brand_el.evaluate('e => getComputedStyle(e).backgroundColor') == 'rgb(25, 181, 222)'
        assert brand_el.evaluate('e => parseFloat(getComputedStyle(e).borderTopRightRadius)') >= 18
        assert brand_el.evaluate('e => parseFloat(getComputedStyle(e).borderBottomRightRadius)') == 0
        assert page.locator('.cenops-about-legal p').first.evaluate('e => getComputedStyle(e).marginBottom') == '0px'
        footer = page.locator('.cenops-about-footer')
        assert footer.evaluate('e => getComputedStyle(e).marginTop') == '0px'
        assert footer.evaluate('e => parseFloat(getComputedStyle(e).paddingBottom)') >= 18
        if width > 700:
            assert brand['x'] > details['x'], 'CENOPS mark must appear to the right of the product details'
            assert abs(brand['height'] - details['height']) < 2, 'Logo box must match the details height'
            assert brand['x'] + brand['width'] > surface.bounding_box()['x'] + surface.bounding_box()['width'] - 2
            assert abs(brand['y'] - surface.bounding_box()['y']) < 2
            assert abs(details['x'] - surface.bounding_box()['x']) < 50
        else:
            assert brand['y'] < details['y'], 'Small-screen mark should stack above the information'
        screen_overflow = page.evaluate('''() => {
          const d=document.querySelector('.app-modal-surface').getBoundingClientRect();
          return d.left < -1 || d.right > innerWidth + 1 || d.top < -1 || d.bottom > innerHeight + 1;
        }''')
        assert not screen_overflow, f'About dialog overflows a {width}x{height} viewport'
        if width == 360:
            # Short screens scroll inside the dialog without pushing it offscreen.
            page.locator('.cenops-about-powered').scroll_into_view_if_needed()
            assert page.locator('.cenops-about-powered').is_visible()
        if width == 1280:
            page.screenshot(path='/mnt/data/cenops261_about_desktop.png', full_page=True)
        if width == 390:
            page.screenshot(path='/mnt/data/cenops261_about_mobile.png', full_page=True)
        page.close()
    browser.close()
print('C.E.N.O.P.S. 2.67 About-layout regression passed (desktop and mobile).')
