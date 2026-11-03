// Portable vector-first graph image export. No dependency on Blazor, a camera,
// browser viewport size or a particular renderer. The caller provides actual
// node positions and SVG paths already computed by its graph layout engine.
const esc = value => String(value ?? '').replace(/[&<>"']/g, char =>
  ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[char]));
const finite = n => Number.isFinite(Number(n)) ? Number(n) : 0;
const rounded = n => Math.round(n * 100) / 100;
const defaultTheme = Object.freeze({ background:'#f6fcfd', dot:'#afd2db', card:'#ffffff', stroke:'#ccdee4',
  title:'#253e4c', muted:'#819da8', accent:'#17aabe', primary:'#008d78', foreign:'#167ea1' });
const clampText = (text, max = 30) => {
  const chars = Array.from(String(text ?? ''));
  return esc(chars.length > max ? chars.slice(0, max - 1).join('') + '…' : text);
};

/** Pure, testable generator: all dimensions are in graph/world units. */
export function buildGraphSvg({ nodes, edges = [], nodeSize = { width:268, height:120 },
  padding = 148, branding = {}, logo = '', theme = defaultTheme } = {}) {
  if (!Array.isArray(nodes) || !nodes.length) throw new Error('نمودار هیچ جدولی برای خروجی ندارد.');
  const width = finite(nodeSize.width), height = finite(nodeSize.height);
  if (width <= 0 || height <= 0) throw new Error('ابعاد گره‌های نمودار نامعتبر است.');
  const left = Math.min(...nodes.map(n => finite(n.x)));
  const top = Math.min(...nodes.map(n => finite(n.y)));
  const right = Math.max(...nodes.map(n => finite(n.x) + width));
  const bottom = Math.max(...nodes.map(n => finite(n.y) + height));
  // Extra space also protects the self-reference curved FK connectors.
  const pad = Math.max(44, finite(padding));
  const footer = 93;
  const minW = 410;
  const contentW = right - left + pad * 2;
  const contentH = bottom - top + pad * 2;
  const documentW = Math.ceil(Math.max(minW, contentW));
  const documentH = Math.ceil(contentH + footer);
  const tx = rounded((documentW - (right - left)) / 2 - left);
  const ty = rounded(pad - top);
  const t = {...defaultTheme,...theme};
  const diagram = [];
  for (const edge of edges) {
    // The graph renderer supplies the same geometries used on screen. Paths are
    // encoded as SVG attributes; reject anything outside SVG path syntax.
    const d = String(edge.d ?? '');
    if (!/^[\s\d.,+\-eEMLCQHVSTAZmlcqhvstaz]*$/.test(d)) continue;
    const color = /^(?:#[0-9a-fA-F]{3,8}|rgba?\([\d.,\s%]+\))$/.test(edge.color) ? edge.color : t.accent;
    diagram.push(`<path d="${esc(d)}" fill="none" stroke="${color}" stroke-width="2" stroke-opacity=".78" stroke-linecap="round"/>`);
  }
  for (const node of nodes) {
    const x = rounded(finite(node.x)), y = rounded(finite(node.y));
    const schema = clampText(node.schema, 28);
    const title = clampText(node.name, 35);
    const badgeY = height - 33;
    const status = (label, x, color, active) =>
      `<rect x="${x}" y="${badgeY}" width="${(width-32)/2}" height="25" rx="7" fill="${active ? '#e1f5f2' : '#fff'}" stroke="${active ? color : t.stroke}"/>` +
      `<text x="${x + (width-32)/4}" y="${badgeY+17}" text-anchor="middle" font-family="Arial,sans-serif" font-size="11" font-weight="700" fill="${color}">${label}${active ? ' ✓' : ''}</text>`;
    diagram.push(`<g transform="translate(${x} ${y})">` +
      `<rect width="${width}" height="${height}" rx="15" fill="${t.card}" stroke="${t.stroke}" stroke-width="1.5"/>` +
      `<path d="M 1 ${height-41} H ${width-1}" stroke="#dcebee"/>` +
      `<text x="14" y="23" fill="${t.accent}" font-family="Arial,sans-serif" font-size="10" font-weight="700">${schema}</text>` +
      `<text x="14" y="47" fill="${t.title}" font-family="Arial,sans-serif" font-size="14" font-weight="700">${title}</text>` +
      `<text x="14" y="64" fill="${t.muted}" font-family="Arial,sans-serif" font-size="9" letter-spacing="1">TABLE</text>` +
      status('← FK',9,t.foreign,node.fkLoaded) + status('PK →',width/2+7,t.primary,node.pkLoaded) + '</g>');
  }
  const logoSize = 53;
  const logoX = documentW - 27 - logoSize;
  const logoY = documentH - 20 - logoSize;
  const logoSvg = logo;
  const brand1 = esc(branding.name ?? 'گمرک · سیناپس');
  const brand2 = esc(branding.version ?? 'C.E.N.O.P.S. · GLASSWORK 2.67');
  // SVGs from the Skyline are internal app assets; strip declarations/doctypes
  // and avoid external image/font dependencies in the self-contained output.
  const safeLogo = String(logoSvg).replace(/<\?xml[^>]*\?>/g,'').replace(/<!DOCTYPE[^>]*>/gi,'');
  return {
    width:documentW,height:documentH,
    svg:`<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${documentW}" height="${documentH}" viewBox="0 0 ${documentW} ${documentH}">
      <defs><pattern id="cenops-export-dots" width="22" height="22" patternUnits="userSpaceOnUse"><circle cx="1.4" cy="1.4" r="1.15" fill="${t.dot}" opacity=".62"/></pattern><clipPath id="cenops-export-clip"><rect x="2" y="2" width="${documentW-4}" height="${documentH-4}" rx="18"/></clipPath></defs>
      <g clip-path="url(#cenops-export-clip)"><rect width="${documentW}" height="${documentH}" fill="${t.background}"/>
      <rect width="${documentW}" height="${documentH-footer}" fill="url(#cenops-export-dots)"/>
      <g transform="translate(${tx} ${ty})">${diagram.join('')}</g>
      <rect x="0" y="${documentH-footer}" width="${documentW}" height="${footer}" fill="#fff" fill-opacity=".91"/>
      <path d="M 26 ${documentH-footer} H ${documentW-26}" stroke="#d4e7eb"/>
      <text x="${logoX-17}" y="${logoY+22}" text-anchor="start" direction="rtl" unicode-bidi="plaintext" fill="#174c60" font-family="Tahoma,Arial,sans-serif" font-weight="700" font-size="16">${brand1}</text>
      <text x="${logoX-17}" y="${logoY+44}" text-anchor="end" fill="#6e96a3" font-family="Arial,sans-serif" font-weight="700" font-size="12">${brand2}</text>
      <g transform="translate(${logoX} ${logoY})"><svg width="${logoSize}" height="${logoSize}" viewBox="0 0 388 388">${extractSvgContent(safeLogo)}</svg></g>
      </g><rect x="1" y="1" width="${documentW-2}" height="${documentH-2}" rx="18" fill="none" stroke="#acd5df" stroke-width="2"/>
    </svg>`
  };
}

// Caller-supplied logo can be a complete SVG. Preserve its actual viewBox as
// well as its children so it renders like the Skyline version, not a new logo.
function extractSvgContent(source) {
  if (!source) return '<rect width="388" height="388" rx="96" fill="#19B5DE"/><text x="194" y="248" font-size="200" fill="#fff" text-anchor="middle">گ</text>';
  const parser = new DOMParser();
  const parsed = parser.parseFromString(source, 'image/svg+xml');
  if (parsed.querySelector('parsererror')) return extractSvgContent('');
  const svg = parsed.documentElement;
  if (svg.localName !== 'svg') return extractSvgContent('');
  const serializer = new XMLSerializer();
  return [...svg.childNodes].map(n => serializer.serializeToString(n)).join('');
}

export async function renderGraphBlob(options, format = 'svg') {
  const result = buildGraphSvg(options);
  const source = new Blob([result.svg], {type:'image/svg+xml;charset=utf-8'});
  if (format === 'svg') return source;
  if (!['png','webp'].includes(format)) throw new Error('فرمت خروجی پشتیبانی نمی‌شود.');
  const budget = 16_000_000, limit = 8192;
  const scale = Math.min(2.4, limit / Math.max(result.width,result.height),
    Math.sqrt(budget / (result.width * result.height)));
  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1,Math.round(result.width * scale));
  canvas.height = Math.max(1,Math.round(result.height * scale));
  // HTMLImageElement consistently decodes stand-alone SVG in Chromium,
  // Firefox and Safari; createImageBitmap(Blob) rejects SVG on some engines.
  const url = URL.createObjectURL(source);
  try {
    const img = new Image();
    img.decoding = 'async';
    await new Promise((resolve,reject) => {
      img.onload = resolve;
      img.onerror = () => reject(new Error('تصویر برداری نمودار قابل پردازش نیست.'));
      img.src = url;
    });
    const ctx = canvas.getContext('2d', {alpha:false});
    if (!ctx) throw new Error('امکان تولید تصویر در مرورگر وجود ندارد.');
    ctx.drawImage(img,0,0,canvas.width,canvas.height);
    const type = format === 'webp' ? 'image/webp' : 'image/png';
    const blob = await new Promise(resolve => canvas.toBlob(resolve,type,format === 'webp' ? .91 : undefined));
    if (!blob || blob.type !== type) throw new Error('مرورگر از این فرمت تصویر پشتیبانی نمی‌کند.');
    return blob;
  } finally { URL.revokeObjectURL(url); canvas.width = canvas.height = 0; }
}
