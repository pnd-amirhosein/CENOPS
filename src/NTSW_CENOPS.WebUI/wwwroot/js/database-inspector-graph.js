import { allRouteEdges, nearEdges } from './graph-paths.js';
import { attachGraphModifierBadges, createGraphHistory } from './graph-interaction-support.js';
import { renderGraphBlob } from './graph-export.js';
// Database Inspector diagram. Independent renderer over the reusable CENOPS graph interaction model.
// Pure DOM/SVG; never insert SQL names as HTML. Supports shared parents, cycles, and user dragging.
const instances = new WeakMap();
const WIDTH = 268, HEIGHT = 120, COLUMN = 374, ROW = 158;
const ns = 'http://www.w3.org/2000/svg';
const edgeColors = ['#0897b5','#8a6ac8','#d17b31','#15876b','#cf6287','#5477cb','#9a8531'];
const relationships = s => s.edges.map(e => ({id:e.id,from:e.parentId,to:e.childId}));
function setSelection(s, id, additive) {
  const before = s.selectedIds.join(':');
  const highlighted = s.highlight !== null;
  if (additive) {
    if (s.selectedIds.includes(id)) s.selectedIds = s.selectedIds.filter(x => x !== id);
    else s.selectedIds.push(id);
  } else s.selectedIds = [id];
  s.selectedId = s.selectedIds.at(-1) ?? null;
  s.highlight = null;
  draw(s);
  if (highlighted || before !== s.selectedIds.join(':')) s.history?.record('انتخاب جدول‌ها');
  s.dotnet.invokeMethodAsync('SelectionChanged', s.selectedIds).catch(console.warn);
  // Selection and inspection are separate: only a double-click opens details.
  s.dotnet.invokeMethodAsync('DismissDetails').catch(console.warn);
}
function dismissSelection(s) {
  const hadSelection = s.selectedIds.length || s.highlight;
  s.selectedId = null; s.selectedIds = []; s.highlight = null;
  draw(s);
  if (hadSelection) s.history?.record('انتخاب جدول‌ها');
  s.dotnet.invokeMethodAsync('SelectionChanged', []).catch(console.warn);
  s.dotnet.invokeMethodAsync('DismissDetails').catch(console.warn);
}

const svgElement = (tag) => document.createElementNS(ns, tag);

function makeButton(label, action, id, active) {
  const button = document.createElement('button');
  button.type = 'button'; button.className = 'dbi-node-action ' + action + (active ? ' loaded' : '');
  button.textContent = label + (active ? ' ✓' : '');
  button.dataset.action = action; button.dataset.id = String(id);
  button.setAttribute('aria-label', action === 'pk' ? 'نمایش جداول فرزند با کلید اصلی' : 'نمایش جداول والد با کلید خارجی');
  return button;
}
// All destructive commands use one topology transaction; Blazor reconciles the dropdown.
function replaceTopology(state, nodes, edges, label = 'تغییر جداول نمودار') {
  if (state.topologyPending) return;
  if (nodes.length === state.nodes.length && edges.length === state.edges.length) return;
  state.topologyPending = true;
  state.pendingTopologyLabel = label;
  state.pendingTopologyKey = topologyKey(nodes, edges);
  state.dotnet.invokeMethodAsync('ReplaceGraph', nodes, edges)
    .catch(console.warn).finally(() => { state.topologyPending = false; });
}
// One-hop ONLY: Ctrl removes direct PK children; Shift removes direct FK parents.
// Nodes reachable beyond the first edge must survive (even in cyclic graphs).
function relatedIds(state, startId, direction) {
  const ids = new Set([startId]);
  for (const edge of state.edges) {
    if (direction === 'pk' && edge.parentId === startId) ids.add(edge.childId);
    if (direction === 'fk' && edge.childId === startId) ids.add(edge.parentId);
  }
  return ids;
}
function removeNodes(state, ids, direction = null) {
  const sources = Array.isArray(ids) ? ids : [ids];
  const removed = new Set();
  for (const id of sources) {
    for (const related of direction ? relatedIds(state, id, direction) : [id]) removed.add(related);
  }
  const nodes = state.nodes.filter(node => !removed.has(node.id));
  const edges = state.edges.filter(edge => !removed.has(edge.parentId) && !removed.has(edge.childId));
  replaceTopology(state, nodes, edges, direction === 'pk' ? 'حذف جدول و فرزندان مستقیم PK' :
    direction === 'fk' ? 'حذف جدول و والدهای مستقیم FK' :
    sources.length > 1 ? `حذف ${sources.length} جدول انتخاب‌شده` : 'حذف جدول');
}
function retainSubgraph(state, subgraph) {
  const nodes = state.nodes.filter(node => subgraph.nodes.has(node.id));
  const edges = state.edges.filter(edge => subgraph.edges.has(edge.id) &&
    subgraph.nodes.has(edge.childId) && subgraph.nodes.has(edge.parentId));
  replaceTopology(state, nodes, edges, 'جداسازی شاخه نمودار');
}
// Box selection uses viewport rectangles, so CSS zoom and camera offsets do not
// require a second coordinate-transform implementation.
function marqueeSelection(state, bounds, additive) {
  const selected = new Set(additive ? state.selectedIds : []);
  for (const card of state.cards.querySelectorAll('.dbi-node')) {
    const r = card.getBoundingClientRect();
    if (r.right > bounds.left && r.left < bounds.right &&
        r.bottom > bounds.top && r.top < bounds.bottom) selected.add(Number(card.dataset.nodeId));
  }
  const before = state.selectedIds.join(':');
  state.selectedIds = [...selected];
  state.selectedId = state.selectedIds.at(-1) ?? null;
  state.highlight = null;
  draw(state);
  if (before !== state.selectedIds.join(':')) state.history.record('انتخاب گروهی جدول‌ها');
  state.dotnet.invokeMethodAsync('SelectionChanged', state.selectedIds).catch(console.warn);
  state.dotnet.invokeMethodAsync('DismissDetails').catch(console.warn);
}
function ensurePosition(state, node) {
  if (state.positions.has(node.id)) return;
  const peers = state.nodes.filter(x => x.level === node.level && state.positions.has(x.id));
  let y = 0;
  if (peers.length) {
    const ys = peers.map(x => state.positions.get(x.id).y).sort((a,b)=>a-b);
    y = ys[ys.length-1] + ROW;
  }
  state.positions.set(node.id, { x: node.level * COLUMN, y });
}
function draw(state) {
  const { canvas, svg, cards } = state;
  cards.replaceChildren(); svg.replaceChildren();
  state.nodes.forEach(node => ensurePosition(state, node));
  const parallel = new Map();
  for (const edge of state.edges) {
    const child = state.positions.get(edge.childId), parent = state.positions.get(edge.parentId);
    if (!child || !parent) continue;
    const pairKey = `${edge.childId}:${edge.parentId}`;
    const lane = parallel.get(pairKey) || 0; parallel.set(pairKey, lane + 1);
    const path = svgElement('path');
    if (edge.childId === edge.parentId) {
      const x = parent.x + WIDTH, y = parent.y;
      path.setAttribute('d', `M ${x-15} ${y+35} C ${x+85+lane*12} ${y-70}, ${x+115+lane*12} ${y+HEIGHT+75}, ${x-15} ${y+HEIGHT-35}`);
    } else {
      const pc = { x: parent.x + WIDTH / 2, y: parent.y + HEIGHT / 2 };
      const cc = { x: child.x + WIDTH / 2, y: child.y + HEIGHT / 2 };
      const right = pc.x <= cc.x;
      const yOffset = Math.min(lane, 5) * 11;
      const a = { x: pc.x + (right ? WIDTH/2 : -WIDTH/2), y: pc.y + yOffset };
      const b = { x: cc.x + (right ? -WIDTH/2 : WIDTH/2), y: cc.y + yOffset };
      const bend = Math.max(50, Math.abs(b.x-a.x)*0.5);
      path.setAttribute('d', `M ${a.x} ${a.y} C ${a.x + (right?bend:-bend)} ${a.y}, ${b.x - (right?bend:-bend)} ${b.y}, ${b.x} ${b.y}`);
    }
    const active = !state.highlight || state.highlight.edges.has(edge.id);
    path.setAttribute('class','dbi-relation-path' + (!active ? ' dimmed' : '') + (state.highlight && active ? ' highlighted' : ''));
    path.style.stroke = edgeColors[Math.abs(edge.id) % edgeColors.length];
    path.setAttribute('data-relation', String(edge.id));
    svg.appendChild(path);
  }
  for (const node of state.nodes) {
    const p = state.positions.get(node.id);
    const card = document.createElement('article');
    card.className = 'dbi-node' + (state.highlight && !state.highlight.nodes.has(node.id) ? ' dimmed' : '') + (state.selectedIds.includes(node.id) ? ' selected' : '');
    card.dataset.nodeId = String(node.id);
    card.style.left = p.x + 'px'; card.style.top = p.y + 'px';
    const heading = document.createElement('div'); heading.className = 'dbi-node-heading';
    const schema = document.createElement('span'); schema.className = 'dbi-node-schema'; schema.textContent = node.schema;
    const title = document.createElement('strong'); title.className = 'dbi-node-name'; title.textContent = node.name;
    const hint = document.createElement('span'); hint.className = 'dbi-node-hint'; hint.textContent = 'TABLE';
    heading.append(schema,title,hint);
    const remove = document.createElement('button');
    remove.type = 'button'; remove.className = 'dbi-node-remove';
    remove.dataset.id = String(node.id);
    remove.title = 'حذف جدول · Ctrl: حذف فرزندان مستقیم PK · Shift: حذف والدهای مستقیم FK';
    remove.setAttribute('aria-label', 'حذف جدول؛ با Ctrl فرزندان مستقیم و با Shift والدهای مستقیم آن نیز حذف می‌شوند');
    remove.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14"/></svg>
      <span class="dbi-remove-direction dbi-remove-children" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M4 12h16m-6-6 6 6-6 6"/></svg></span>
      <span class="dbi-remove-direction dbi-remove-parents" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M20 12H4m6-6-6 6 6 6"/></svg></span>`;
    const actions = document.createElement('div'); actions.className = 'dbi-node-actions';
    actions.append(makeButton('← FK','fk',node.id,node.fkLoaded),makeButton('PK →','pk',node.id,node.pkLoaded));
    const surface = document.createElement('div'); surface.className = 'dbi-node-surface';
    surface.append(heading, actions);
    card.append(surface, remove); cards.appendChild(card);
  }
  setCamera(state);
}
// Keep zoom in layout space so text and SVG are re-rasterized at native resolution.
// Avoid GPU transform compositing of the entire canvas (which blurs text at high zoom).
function setCamera(state) {
  const ratio = window.devicePixelRatio || 1;
  state.pan.style.left = `${Math.round(state.offset.x * ratio) / ratio}px`;
  state.pan.style.top = `${Math.round(state.offset.y * ratio) / ratio}px`;
  state.canvas.style.zoom = String(state.scale);
}
function fitVisible(state) {
  const active = state.nodes;
  if(!active.length) return;
  const left=Math.min(...active.map(n=>state.positions.get(n.id).x));
  const top=Math.min(...active.map(n=>state.positions.get(n.id).y));
  const right=Math.max(...active.map(n=>state.positions.get(n.id).x+WIDTH));
  const bottom=Math.max(...active.map(n=>state.positions.get(n.id).y+HEIGHT));
  state.scale=Math.max(.35, Math.min(1.6,(state.stage.clientWidth-80)/Math.max(1,right-left),(state.stage.clientHeight-80)/Math.max(1,bottom-top)));
  state.offset={x:(state.stage.clientWidth-(left+right)*state.scale)/2,y:(state.stage.clientHeight-(top+bottom)*state.scale)/2};
  setCamera(state);
}
function tidyVisibleLayout(state) {
  const groups = new Map();
  for (const node of state.nodes) {
    if (!groups.has(node.level)) groups.set(node.level, []);
    groups.get(node.level).push(node);
  }
  [...groups.keys()].sort((a, b) => a - b).forEach((level, column) => {
    groups.get(level).sort((a, b) => state.positions.get(a.id).y - state.positions.get(b.id).y)
      .forEach((node, row) => state.positions.set(node.id, {x: column * COLUMN, y: row * ROW}));
  });
}

const cloneSubgraph = graph => graph && {
  nodes: new Set(graph.nodes), edges: new Set(graph.edges)
};
function captureState(s) {
  return {
    nodes: s.nodes, edges: s.edges,
    positions: [...s.positions].map(([id, point]) => [id, {...point}]),
    scale: s.scale, offset: {...s.offset}, selectedIds: [...s.selectedIds],
    highlight: cloneSubgraph(s.highlight)
  };
}
function topologyKey(nodes, edges) {
  return nodes.map(n => `${n.id}:${n.level}:${n.pkLoaded}:${n.fkLoaded}`).join('|') + ' / ' +
    edges.map(e => `${e.id}:${e.childId}:${e.parentId}`).join('|');
}
function restoreState(s, snapshot) {
  const changed = topologyKey(s.nodes, s.edges) !== topologyKey(snapshot.nodes, snapshot.edges);
  s.nodes = snapshot.nodes; s.edges = snapshot.edges;
  s.positions = new Map(snapshot.positions.map(([id, point]) => [id, {...point}]));
  s.scale = snapshot.scale; s.offset = {...snapshot.offset};
  s.selectedIds = [...snapshot.selectedIds];
  s.selectedId = s.selectedIds.at(-1) ?? null;
  s.highlight = cloneSubgraph(snapshot.highlight);
  s.restoringTopology = changed ? topologyKey(snapshot.nodes, snapshot.edges) : null;
  draw(s);
  s.dotnet.invokeMethodAsync('SelectionChanged', s.selectedIds).catch(console.warn);
  s.dotnet.invokeMethodAsync('DismissDetails').catch(console.warn);
  if (changed) s.dotnet.invokeMethodAsync('RestoreGraph', snapshot.nodes, snapshot.edges).catch(console.warn);
}
function recordCameraGesture(s) {
  clearTimeout(s.cameraTimer);
  s.cameraTimer = setTimeout(() => { s.cameraTimer = null; s.history.record('بزرگ‌نمایی یا جابه‌جایی'); }, 180);
}
function finishCameraGesture(s) {
  if (s.cameraTimer !== null) {
    clearTimeout(s.cameraTimer); s.cameraTimer = null; s.history.record('بزرگ‌نمایی یا جابه‌جایی');
  }
}
function resetCamera(state) {
  // Fit the selected root tables into the initial viewport when possible.
  const roots = state.nodes.filter(n => n.level === 0).length;
  const span = Math.max(HEIGHT, (roots - 1) * ROW + HEIGHT);
  state.scale = Math.max(.35, Math.min(1, (state.stage.clientHeight - 64) / span));
  state.offset = {
    x: state.stage.clientWidth / 2 - WIDTH * state.scale / 2,
    y: state.stage.clientHeight / 2 - span * state.scale / 2
  };
  setCamera(state);
}
export function mount(root, nodes, edges, dotnet, focusedRootIds = null) {
  unmount(root);
  const stage = root.querySelector('.dbi-graph-stage');
  if (!stage) return;
  const pan = document.createElement('div'); pan.className='dbi-pan';
  const canvas = document.createElement('div'); canvas.className='dbi-canvas';
  const svg = svgElement('svg'); svg.setAttribute('class','dbi-edges'); svg.setAttribute('aria-hidden','true');
  const cards=document.createElement('div'); cards.className='dbi-nodes';
  const marquee=document.createElement('div'); marquee.className='dbi-marquee';
  canvas.append(svg,cards); pan.appendChild(canvas); stage.append(pan,marquee);
  const state={root,stage,pan,canvas,svg,cards,marquee,dotnet,nodes,edges,positions:new Map(),scale:1,
    offset:{x:0,y:0},pointer:null,suppressClick:false,disposed:false,selectedId:null,selectedIds:[],highlight:null,
    cameraTimer:null,restoringTopology:null,lastTap:null,topologyPending:false,pendingTopologyLabel:null,pendingTopologyKey:null,
    focusedId:(focusedRootIds ?? nodes.filter(n=>n.level===0).map(n=>n.id)).slice().sort((a,b)=>a-b).join(':')};
  instances.set(root,state);
  // Live marquee preview is ephemeral; it never touches selection or history.
  const previewMarquee = bounds => {
    for (const card of cards.querySelectorAll('.dbi-node')) {
      const r = card.getBoundingClientRect();
      card.classList.toggle('marquee-preview', r.right > bounds.left && r.left < bounds.right &&
        r.bottom > bounds.top && r.top < bounds.bottom);
    }
  };
  const clearPreview = () => cards.querySelectorAll('.marquee-preview').forEach(card => card.classList.remove('marquee-preview'));
  const down = event => {
    if (event.button !== 0 || event.target.closest('button')) return;
    const node = event.target.closest('.dbi-node');
    const additive = event.ctrlKey || event.metaKey;
    const mode = node ? 'node' : additive ? 'marquee' : 'pan';
    state.pointer={id:event.pointerId,x:event.clientX,y:event.clientY,
      startX:event.clientX,startY:event.clientY,moved:false,
      mode,nodeId:node ? Number(node.dataset.nodeId) : null,additive};
    stage.focus({preventScroll: true});
    stage.setPointerCapture(event.pointerId);
    if (mode === 'marquee') event.preventDefault();
  };
  const move = event => {
    const p=state.pointer;
    if (!p || p.id !== event.pointerId) return;
    const dx=event.clientX-p.x,dy=event.clientY-p.y;
    if(Math.abs(event.clientX-p.startX)+Math.abs(event.clientY-p.startY)>5)p.moved=true;
    if (p.moved) {
      if (p.mode === 'node') {
        const loc=state.positions.get(p.nodeId);
        if(loc) {loc.x+=dx/state.scale;loc.y+=dy/state.scale;draw(state);}
      } else if (p.mode === 'pan') {
        state.offset.x+=dx;state.offset.y+=dy;setCamera(state);
      } else {
        const rect=stage.getBoundingClientRect();
        state.marquee.style.left = `${Math.min(p.startX,event.clientX)-rect.left}px`;
        state.marquee.style.top = `${Math.min(p.startY,event.clientY)-rect.top}px`;
        state.marquee.style.width = `${Math.abs(event.clientX-p.startX)}px`;
        state.marquee.style.height = `${Math.abs(event.clientY-p.startY)}px`;
        state.marquee.classList.add('active');
        previewMarquee({left:Math.min(p.startX,event.clientX),right:Math.max(p.startX,event.clientX),
          top:Math.min(p.startY,event.clientY),bottom:Math.max(p.startY,event.clientY)});
      }
    }
    p.x=event.clientX;p.y=event.clientY;
  };
  const up = event => {
    const p=state.pointer;
    if (!p || p.id!==event.pointerId)return;
    // Redrawing cards on pointer-up can suppress the browser's native click.
    // Store a *specific* click to ignore, never a sticky flag that consumes a
    // later toolbar/remove action when no native click was dispatched.
    state.suppressClick = p.moved || p.nodeId !== null
      ? {time:event.timeStamp,x:event.clientX,y:event.clientY} : null;
    state.marquee.classList.remove('active');
    clearPreview();
    if (p.moved && p.mode === 'marquee' && event.type !== 'pointercancel') {
      marqueeSelection(state, {
        left:Math.min(p.startX,event.clientX),right:Math.max(p.startX,event.clientX),
        top:Math.min(p.startY,event.clientY),bottom:Math.max(p.startY,event.clientY)
      },p.additive);
    } else if (p.moved && p.mode !== 'marquee') state.history.record(p.mode === 'node' ? 'جابه‌جایی جدول' : 'جابه‌جایی نمای نمودار');
    if (!p.moved && p.nodeId !== null && event.type !== 'pointercancel') {
      const previous = state.lastTap;
      const doubleClick = !p.additive && previous?.id === p.nodeId &&
        event.timeStamp - previous.time < 450 &&
        Math.abs(event.clientX - previous.x) + Math.abs(event.clientY - previous.y) < 12;
      setSelection(state,p.nodeId,p.additive);
      state.lastTap = doubleClick ? null : {id:p.nodeId,time:event.timeStamp,x:event.clientX,y:event.clientY};
      if (doubleClick) state.dotnet.invokeMethodAsync('SelectTable',p.nodeId).catch(console.warn);
    } else state.lastTap = null;
    state.pointer=null;
    if(stage.hasPointerCapture(event.pointerId))stage.releasePointerCapture(event.pointerId);
  };
  const click = event => {
    const suppressed = state.suppressClick;
    state.suppressClick = null;
    const buttonTarget = event.target.closest('button');
    if (!buttonTarget && suppressed && event.timeStamp - suppressed.time < 140 &&
        Math.abs(event.clientX - suppressed.x) + Math.abs(event.clientY - suppressed.y) < 8)
      return; // Selection and double-click were already resolved on pointer-up.
    const remove=event.target.closest('.dbi-node-remove');
    if (remove) {
      const direction = event.shiftKey ? 'fk' : (event.ctrlKey || event.metaKey) ? 'pk' : null;
      removeNodes(state,Number(remove.dataset.id),direction);
      return;
    }
    const button=event.target.closest('.dbi-node-action');
    if(button){const id=Number(button.dataset.id);
      state.dotnet.invokeMethodAsync(button.dataset.action==='pk'?'ExpandPrimary':'ExpandForeign',id);
      return;
    }
    const card=event.target.closest('.dbi-node');
    if(card){setSelection(state,Number(card.dataset.nodeId),event.ctrlKey||event.metaKey);}
    else {
      dismissSelection(state);
      if (root.querySelector('.dbi-fk-menu')) state.dotnet.invokeMethodAsync('DismissForeignMenu');
    }
  };
  const wheel = event => {
    event.preventDefault();
    if(event.ctrlKey || event.metaKey){
      const rect=stage.getBoundingClientRect();
      const mx=event.clientX-rect.left, my=event.clientY-rect.top;
      const factor=event.deltaY<0?1.12:1/1.12;
      const next=Math.max(.35,Math.min(3.5,state.scale*factor));
      const wx=(mx-state.offset.x)/state.scale,wy=(my-state.offset.y)/state.scale;
      state.offset.x=mx-wx*next;state.offset.y=my-wy*next;state.scale=next;
    } else if(event.shiftKey){state.offset.x-=event.deltaY;} else {state.offset.y-=event.deltaY;state.offset.x-=event.deltaX;}
    setCamera(state);
    recordCameraGesture(state);
  };
  const keydown = event => {
    const active = document.activeElement;
    const editing = active?.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(active?.tagName);
    if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey &&
        event.key.toLowerCase() === 'a' && !editing &&
        (root.contains(active) || document.fullscreenElement === root)) {
      event.preventDefault();
      finishCameraGesture(state);
      const allIds = state.nodes.map(node => node.id);
      if (allIds.length && (allIds.length !== state.selectedIds.length ||
          allIds.some(id => !state.selectedIds.includes(id)))) {
        state.selectedIds = allIds;
        state.selectedId = allIds.at(-1);
        state.highlight = null;
        draw(state);
        state.history.record('انتخاب تمام جدول‌ها');
        state.dotnet.invokeMethodAsync('SelectionChanged', [...allIds]).catch(console.warn);
        state.dotnet.invokeMethodAsync('DismissDetails').catch(console.warn);
      }
      return;
    }
    if (event.key === 'Backspace' && !event.ctrlKey && !event.metaKey && !event.altKey &&
        !editing && stage.contains(active) && state.selectedIds.length) {
      event.preventDefault(); finishCameraGesture(state);
      removeNodes(state, [...state.selectedIds]);
      return;
    }
    if ((event.ctrlKey || event.metaKey) && !event.altKey &&
        (event.key.toLowerCase() === 'z' || event.key.toLowerCase() === 'y') &&
        root.contains(document.activeElement) &&
        !editing) {
      event.preventDefault(); finishCameraGesture(state);
      if (event.key.toLowerCase() === 'y' || event.shiftKey) state.history.redo();
      else state.history.undo();
      return;
    }
    if(event.key==='Escape' && root.querySelector('.dbi-fk-menu')) {
      event.preventDefault();state.dotnet.invokeMethodAsync('DismissForeignMenu');
    }
  };
  // Browser popup activation expires during an async Blazor server event. Open
  // the preview tab on the original DOM click, then export its graph snapshot.
  const exportClick = event => {
    const item = event.target.closest('[data-menu-value]');
    if (!item || !root.contains(item) || item.disabled) return;
    const match = /^image:(png|svg|webp)$/.exec(item.dataset.menuValue ?? '');
    if (!match) return;
    if (!state.nodes.length) {
      state.dotnet.invokeMethodAsync('ExportFailed', 'نمودار هیچ جدولی برای خروجی ندارد.').catch(console.warn);
      return;
    }
    const tab = window.open('about:blank', '_blank');
    if (!tab) {
      state.dotnet.invokeMethodAsync('ExportFailed', 'مرورگر پنجره جدید را مسدود کرده است. لطفاً اجازه بازشدن پنجره را بدهید.').catch(console.warn);
      return;
    }
    // Detached from the originating page for safety, but still navigable here.
    tab.opener = null;
    tab.document.title = 'CENOPS · Graph Export';
    tab.document.body.textContent = 'در حال آماده‌سازی تصویر نمودار…';
    const options = {
      nodes: state.nodes.map(node => ({...node, ...state.positions.get(node.id)})),
      edges: [...state.svg.querySelectorAll('.dbi-relation-path')].map(path => ({
        d:path.getAttribute('d'), color:path.style.stroke
      })),
      nodeSize: {width:WIDTH,height:HEIGHT},
      logo: document.querySelector('.skyline-logo svg')?.outerHTML ?? '',
      branding: {name:'گمرک · سیناپس',version:'C.E.N.O.P.S. · GLASSWORK 2.67'}
    };
    renderGraphBlob(options, match[1]).then(blob => {
      if (tab.closed) return;
      const url = URL.createObjectURL(blob);
      tab.location.replace(url);
      // Give the preview plenty of time to load and be saved from its new tab.
      setTimeout(() => URL.revokeObjectURL(url), 600_000);
    }).catch(error => {
      if (!tab.closed) tab.document.body.textContent = 'خروجی نمودار ایجاد نشد.';
      console.error('Graph export:', error);
      state.dotnet.invokeMethodAsync('ExportFailed', 'ساخت خروجی نمودار انجام نشد. دوباره تلاش کنید.').catch(console.warn);
    });
  };
  root.addEventListener('click', exportClick, true);
  const detachModifiers = attachGraphModifierBadges(root);
  const closeHistoryOnOutside = event => {
    if (root.querySelector('.graph-history-popover') &&
        !event.target.closest('.graph-history-trigger, .graph-history-popover'))
      state.dotnet.invokeMethodAsync('DismissHistory').catch(console.warn);
  };
  document.addEventListener('pointerdown', closeHistoryOnOutside);
  document.addEventListener('keydown',keydown);
  stage.addEventListener('pointerdown',down);stage.addEventListener('pointermove',move);
  stage.addEventListener('pointerup',up);stage.addEventListener('pointercancel',up);
  stage.addEventListener('click',click);stage.addEventListener('wheel',wheel,{passive:false});
  state.cleanup=()=>{clearTimeout(state.cameraTimer);detachModifiers();
    root.removeEventListener('click', exportClick, true);
    document.removeEventListener('pointerdown',closeHistoryOnOutside);
    document.removeEventListener('keydown',keydown);stage.removeEventListener('pointerdown',down);stage.removeEventListener('pointermove',move);
    stage.removeEventListener('pointerup',up);stage.removeEventListener('pointercancel',up);
    stage.removeEventListener('click',click);stage.removeEventListener('wheel',wheel);};
  resetCamera(state);draw(state);
  state.history = createGraphHistory(() => captureState(state), snapshot => restoreState(state, snapshot),
    (canUndo, canRedo, entries, cursor) => state.dotnet.invokeMethodAsync('HistoryChanged', canUndo, canRedo, entries, cursor).catch(console.warn));
}
export function update(root,nodes,edges,focusedRootIds = null){
  const state=instances.get(root);if(!state)return;
  const nextFocus = (focusedRootIds ?? nodes.filter(n=>n.level===0).map(n=>n.id)).slice().sort((a,b)=>a-b).join(':');
  const nextTopology = topologyKey(nodes, edges);
  const newTopology = topologyKey(state.nodes, state.edges) !== nextTopology;
  const restored = state.restoringTopology === nextTopology;
  state.restoringTopology = null;
  const previousSelection = state.selectedIds.join(':');
  state.nodes=nodes;state.edges=edges;
  // Root selection belongs to the graph topology and is undoable. In particular,
  // clearing the last node must NOT destroy the preceding history or view positions.
  state.focusedId = nextFocus;
  if (nodes.length && !state.positions.size) resetCamera(state);
  if (!restored) state.highlight=null;
  state.selectedIds=state.selectedIds.filter(id=>nodes.some(n=>n.id===id));
  state.selectedId=state.selectedIds.at(-1) ?? null;
  const keep=new Set(nodes.map(n=>n.id));
  for(const id of state.positions.keys()) if(!keep.has(id))state.positions.delete(id);
  draw(state);
  if (previousSelection !== state.selectedIds.join(':')) {
    state.dotnet.invokeMethodAsync('SelectionChanged', state.selectedIds).catch(console.warn);
    state.dotnet.invokeMethodAsync('DismissDetails').catch(console.warn);
  }
  if (newTopology && !restored) {
    state.history.record(state.pendingTopologyKey === nextTopology
      ? state.pendingTopologyLabel : 'تغییر جداول و ارتباط‌ها');
    state.pendingTopologyLabel = null;
    state.pendingTopologyKey = null;
  }
}
export function zoom(root,factor){const s=instances.get(root);if(s){s.scale=Math.max(.35,Math.min(3.5,s.scale*factor));setCamera(s);recordCameraGesture(s);}}
export function goToHistory(root,index){const s=instances.get(root);if(s){finishCameraGesture(s);s.history.goTo(index);}}
export function undo(root){const s=instances.get(root);if(s){finishCameraGesture(s);s.history.undo();}}
export function redo(root){const s=instances.get(root);if(s){finishCameraGesture(s);s.history.redo();}}
// Reset is a VIEW tidy only. Preserve revealed tables, isolation and current selection.
export function reset(root){const s=instances.get(root);if(!s)return;
  finishCameraGesture(s);s.highlight=null;tidyVisibleLayout(s);draw(s);fitVisible(s);s.history.record('بازچینی و تنظیم نما');
}
export function clearWorkspace(root){const s=instances.get(root);if(!s)return;
  finishCameraGesture(s);replaceTopology(s,[],[],'پاک‌سازی کل نمودار');
}
export async function fullscreen(root){if(document.fullscreenElement===root)await document.exitFullscreen();else if(root.requestFullscreen)await root.requestFullscreen();}
// Place the relationship picker inside the visible stage. Prefer the space below
// the action; flip above when necessary, and let its options scroll on short stages.
export function positionMenu(root,id,primary=false){
  const action = primary ? 'pk' : 'fk';
  const button=root.querySelector(`.dbi-node-action.${action}[data-id="${Number(id)}"]`);
  const menu=root.querySelector('.dbi-fk-menu');
  if(!menu||!button)return;
  const padding=10, gap=8;
  const bounds=root.getBoundingClientRect(),anchor=button.getBoundingClientRect();
  const width=Math.min(menu.offsetWidth,Math.max(0,bounds.width-padding*2));
  const maxHeight=Math.max(0,bounds.height-padding*2);
  menu.style.maxHeight=`${maxHeight}px`;
  menu.style.overflow='auto';
  const height=menu.getBoundingClientRect().height;
  const below=anchor.bottom-bounds.top+gap;
  const above=anchor.top-bounds.top-gap-height;
  const roomBelow=bounds.height-padding-below;
  const roomAbove=anchor.top-bounds.top-gap-padding;
  const y=roomBelow>=height || roomBelow>=roomAbove ? below : above;
  const left=anchor.left-bounds.left;
  menu.style.left=`${Math.max(padding,Math.min(bounds.width-padding-width,left))}px`;
  menu.style.top=`${Math.max(padding,Math.min(bounds.height-padding-height,y))}px`;
}
export function unmount(root){const s=instances.get(root);if(!s)return;s.cleanup?.();s.pan.remove();s.marquee.remove();instances.delete(root);}

// Graph controls: no camera mutation in normal highlight mode.
export function highlightPath(root, prune = false) {
  const s=instances.get(root);
  if (!s || s.selectedIds.length!==2) return false;
  const active=s.nodes.map(n=>n.id);
  const route=allRouteEdges(active,relationships(s),s.selectedIds[0],s.selectedIds[1]);
  if(!route) return false; // Never alter the previous view on a failed route.
  if(prune) {retainSubgraph(s,route);return true;}
  else {s.highlight=route;draw(s);}
  s.history.record('نمایش مسیرهای بین جدول‌ها');
  return true;
}
export function focusBranch(root, prune = false) {
  const s=instances.get(root);
  if(!s || s.selectedIds.length !== 1)return;
  const near=nearEdges(relationships(s),s.selectedIds.at(-1));
  if(prune) {retainSubgraph(s,near);return;}
  else {s.highlight=near;draw(s);}
  s.history.record('برجسته‌سازی ارتباط‌های نزدیک');
}
export function clearSelection(root) {
  const s=instances.get(root);if(!s)return;
  s.selectedId=null;s.selectedIds=[];s.highlight=null;draw(s);s.history.record('پاک کردن انتخاب');
  s.dotnet.invokeMethodAsync('SelectionChanged',[]).catch(console.warn);
}

// A modal hosted by the application overlay must not render beneath the browser's fullscreen top layer.
export async function leaveFullscreenForDialog(root) {
  if (document.fullscreenElement === root) await document.exitFullscreen();
}
