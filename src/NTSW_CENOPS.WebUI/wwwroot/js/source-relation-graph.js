import { allRouteEdges, nearEdges } from './graph-paths.js';
import { attachGraphModifierBadges } from './graph-interaction-support.js';
// CENOPS reusable source graph. DOM text is always textContent (never source-derived HTML).
// Each vertex is a real enclosing declaration; graph expansion fetches only that declaration's direct references.
const SVG = 'http://www.w3.org/2000/svg';
const instances = new WeakMap();
const clamp = (n, min, max) => Math.min(max, Math.max(min, n));
function svg(tag, attrs = {}) {
    const node = document.createElementNS(SVG, tag);
    for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, String(v));
    return node;
}
function label(value, attrs) { const node = svg('text', attrs); node.textContent = value || ''; return node; }
const palette = kind => ({
    Definition: {accent:'#068d9b',fill:'#e1f7f8',stroke:'#8bced6'},
    Class: {accent:'#4f78d6',fill:'#eef3ff',stroke:'#b8caf7'},
    Record: {accent:'#4f78d6',fill:'#eef3ff',stroke:'#b8caf7'},
    Struct: {accent:'#4f78d6',fill:'#eef3ff',stroke:'#b8caf7'},
    Interface: {accent:'#805dbd',fill:'#f5f0ff',stroke:'#cec1ec'},
    Method: {accent:'#078f82',fill:'#eaf9f4',stroke:'#9fdad1'},
    Constructor: {accent:'#078f82',fill:'#eaf9f4',stroke:'#9fdad1'},
    LocalFunction: {accent:'#078f82',fill:'#eaf9f4',stroke:'#9fdad1'},
    Property: {accent:'#aa7330',fill:'#fff6e9',stroke:'#e8ce9d'},
    Field: {accent:'#aa7330',fill:'#fff6e9',stroke:'#e8ce9d'},
    Unresolved: {accent:'#78838e',fill:'#f4f7f9',stroke:'#cad4db'}
})[kind] || {accent:'#687aa4',fill:'#f3f5fc',stroke:'#c2cee6'};

function hierarchy(nodes, edges) {
    const children = new Map(nodes.map(n => [n.id, []]));
    const parent = new Map();
    for (const e of edges) {
        if (!children.has(e.from) || !children.has(e.to) || parent.has(e.to)) continue;
        children.get(e.from).push(e.to);
        parent.set(e.to, e.from);
    }
    return {children, parent};
}
function ancestry(state, id) {
    const path = new Set();
    while (id && !path.has(id)) { path.add(id); id = state.parent.get(id); }
    return path;
}
function descendants(state, id) {
    const seen = new Set(); const queue = [id];
    while (queue.length) {
        const next = queue.shift(); if (seen.has(next)) continue;
        seen.add(next);
        for (const child of state.children.get(next) || []) queue.push(child);
    }
    return seen;
}
function visible(state) {
    const root = state.nodes[0]?.id;
    if (!root) return new Set();
    const shown = new Set();
    function visit(id) {
        if (shown.has(id)) return;
        shown.add(id);
        if (state.expanded.has(id)) (state.children.get(id) || []).forEach(visit);
    }
    visit(root);
    return state.isolate ? new Set([...shown].filter(id=>state.isolate.nodes.has(id))) : shown;
}
// Render full file/member names by wrapping text to a bounded card width.
// Geometry is calculated from the exact lines rendered; no CSS ellipsis or clipping.
function wrapped(state, text, maxPixels, font = '700 12px Consolas, monospace') {
    const ctx = state.measure; ctx.font = font;
    const words = String(text || '').split(/(?<=[.\/\\:_-])|(?<=\s)/u).filter(Boolean);
    const lines = []; let current = '';
    for (const word of words) {
        if (ctx.measureText(current + word).width <= maxPixels) { current += word; continue; }
        if (current) { lines.push(current.trimEnd()); current = ''; }
        // Long unbroken identifiers are split by measured characters, never truncated.
        for (const char of word) {
            if (current && ctx.measureText(current + char).width > maxPixels) {
                lines.push(current); current = '';
            }
            current += char;
        }
    }
    if (current) lines.push(current.trimEnd());
    return lines.length ? lines : [''];
}
function nodeLayout(state, node) {
    const cached = state.dimensions.get(node.id); if (cached) return cached;
    const width = 355, contentWidth = width - 43;
    const title = wrapped(state, node.title, contentWidth, '700 13px Consolas, monospace');
    const subtitle = wrapped(state, node.subtitle, contentWidth, '11px Tahoma, sans-serif');
    const titleStart = 53;
    const subtitleStart = titleStart + title.length * 19 + 8;
    const height = Math.max(109, subtitleStart + subtitle.length * 15 + 17);
    const result = {width, height, title, subtitle, titleStart, subtitleStart};
    state.dimensions.set(node.id, result);
    return result;
}
function nodeWidth(state, node) { return nodeLayout(state,node).width; }
function arrange(state) {
    const ids=visible(state);state.visible=ids;
    const shown=state.nodes.filter(n=>ids.has(n.id));
    const levels=[...new Set(shown.map(n=>n.level))].sort((a,b)=>a-b);
    const widths=new Map(levels.map(l=>[l,Math.max(...shown.filter(n=>n.level===l).map(n=>nodeWidth(state,n)))]));
    const x=new Map();let nextX=60;
    for(const level of levels){x.set(level,nextX);nextX+=widths.get(level)+96;}
    const positions=new Map();let nextLeafY=45;
    const visited=new Set();
    function place(id){
        if(!ids.has(id)||visited.has(id))return null;
        visited.add(id);
        const children=(state.children.get(id)||[]).filter(c=>ids.has(c));
        const childYs=children.map(place).filter(v=>Number.isFinite(v));
        const geometry=nodeLayout(state,state.byId.get(id));
        const y=childYs.length?(childYs[0]+childYs[childYs.length-1])/2-geometry.height/2:
            (nextLeafY += geometry.height+27)-geometry.height-27;
        positions.set(id,{x:x.get(state.byId.get(id).level)||60,y});
        return y + geometry.height/2;
    }
    place(state.nodes[0]?.id);
    for(const node of shown) if(!visited.has(node.id))place(node.id);
    state.positions=positions;
}
function fit(state) {
    const nodes = [...state.positions];
    if (!nodes.length) { state.panX=0;state.panY=0;state.scale=1;transform(state);return; }
    const left=Math.min(...nodes.map(([,p])=>p.x)), top=Math.min(...nodes.map(([,p])=>p.y));
    const right=Math.max(...nodes.map(([id,p])=>p.x+nodeWidth(state,state.byId.get(id))));
    const bottom=Math.max(...nodes.map(([id,p])=>p.y+nodeLayout(state,state.byId.get(id)).height));
    const w=state.stage.clientWidth||900,h=state.stage.clientHeight||550;
    state.scale=clamp(Math.min((w-80)/Math.max(1,right-left),(h-85)/Math.max(1,bottom-top)),.12,1.35);
    state.panX=(w-(left+right)*state.scale)/2;
    state.panY=(h-(top+bottom)*state.scale)/2;
    transform(state);
}
function transform(state) {
    state.viewport.setAttribute('transform',`translate(${state.panX},${state.panY}) scale(${state.scale})`);
}
function links(state) {
    const positions=state.positions;
    for (const [edge,path] of state.paths) {
        const a=positions.get(edge.from),b=positions.get(edge.to);
        if (!a||!b) continue;
        const from=a.x+nodeWidth(state,state.byId.get(edge.from)), to=b.x;
        const ctrl=Math.max(35,(to-from)/2);
        const ay=a.y+nodeLayout(state,state.byId.get(edge.from)).height/2;
        const by=b.y+nodeLayout(state,state.byId.get(edge.to)).height/2;
        path.setAttribute('d',`M${from},${ay} C${from+ctrl},${ay} ${to-ctrl},${by} ${to},${by}`);
    }
}
const sourceColors = ['#258fa4','#6585d9','#9a6ac0','#168f77','#bd823a'];
function graphLinks(state) {
    return state.edges.map((e,i)=>({id:i,from:e.from,to:e.to}))
        .filter(e=>state.visible.has(e.from)&&state.visible.has(e.to) && (!state.isolate || state.isolate.edges.has(e.id)));
}
function paint(state) {
    for (const [id, group] of state.groups) {
        const node=state.byId.get(id), selected=state.selectedIds.includes(id);
        group.style.opacity=state.highlight && !state.highlight.nodes.has(id)?'0.17':'1';
        const rect=group.querySelector('.graph-node-body');
        if (rect) {
            const theme=palette(node.kind);
            rect.setAttribute('fill',selected?'#d8f3f1':theme.fill);
            rect.setAttribute('stroke',selected?'#078c96':theme.stroke);
            rect.setAttribute('stroke-width',selected?'2.4':'1.2');
            group.classList.toggle('is-selected',selected);
        }
    }
    for(const [edge,path,index] of state.paths) {
        const active=!state.highlight||state.highlight.edges.has(index);
        const mediatR=/MediatR/i.test(edge.kind||'');
        path.style.opacity=active?(state.highlight?'1':'.8'):'.10';
        path.setAttribute('stroke',mediatR?'#cc8735':sourceColors[index%sourceColors.length]);
        path.setAttribute('stroke-width',active&&state.highlight?'3':'1.8');
    }
}
function tooltip(state,node,ev) {
    const tt=state.tooltip;tt.replaceChildren();
    const header=document.createElement('div');header.className='graph-tooltip-header';
    const kind=document.createElement('span');kind.className='graph-tooltip-kind';kind.textContent=node.kind;
    const count=document.createElement('span');count.className='graph-tooltip-count';
    count.textContent=node.referenceCount ? `${node.referenceCount} reference${node.referenceCount===1?'':'s'}` : 'Source symbol';
    header.append(kind,count);tt.append(header);
    const head=document.createElement('strong');head.textContent=node.title;tt.append(head);
    if(node.subtitle){const owner=document.createElement('div');owner.className='graph-tooltip-owner';owner.textContent=node.subtitle;tt.append(owner);}
    if(/MediatR/i.test(node.relationship||'')){const framework=document.createElement('span');framework.className='graph-tooltip-framework';framework.textContent='MediatR · inferred dispatch';tt.append(framework);}
    const info=document.createElement('div');info.className='graph-tooltip-metadata';
    info.textContent=[node.project, node.file && `${node.file}${node.line ? `:${node.line}` : ''}`,
        node.namespace && `Namespace: ${node.namespace}`].filter(Boolean).join('\n');tt.append(info);
    if(node.code){const code=document.createElement('code');code.textContent=node.code.split('\n').slice(0,3).join('\n');tt.append(code);}
    tt.style.display='grid';
    const stage=state.stage.getBoundingClientRect();
    // Position using the *actual* tooltip dimensions, not a guessed fixed height.
    // A long snippet can never escape the graph viewport or overlap its own box.
    const rect=tt.getBoundingClientRect();
    tt.style.left=`${clamp(ev.clientX-stage.left+14,8,Math.max(8,stage.width-rect.width-8))}px`;
    tt.style.top=`${clamp(ev.clientY-stage.top+15,8,Math.max(8,stage.height-rect.height-8))}px`;
}
function draw(state) {
    state.viewport.replaceChildren();state.groups=new Map();state.paths=[];
    const edgeLayer=svg('g'),nodeLayer=svg('g');
    for(const [index,edge] of state.edges.entries()) {
        if(!state.visible.has(edge.from)||!state.visible.has(edge.to) || (state.isolate && !state.isolate.edges.has(index)))continue;
        const path=svg('path',{class:'graph-edge',fill:'none',stroke:'#8ebac7','stroke-width':1.8});
        if(/MediatR/i.test(edge.kind||'')) {
            path.setAttribute('stroke-dasharray','7 5');
            const description=svg('title');description.textContent='MediatR dispatch (inferred from request type)';
            path.append(description);
        }
        edgeLayer.append(path);state.paths.push([edge,path,index]);
    }
    for(const node of state.nodes) {
        if(!state.visible.has(node.id))continue;
        const p=state.positions.get(node.id),box=nodeLayout(state,node),w=box.width,h=box.height,theme=palette(node.kind);
        const group=svg('g',{class:'graph-vertex','data-node-id':node.id,role:'button',tabindex:0,
            'aria-label':`${node.kind}: ${node.title}`,transform:`translate(${p.x},${p.y})`});
        group.style.cursor='grab';
        group.append(svg('rect',{class:'graph-node-body',width:w,height:h,rx:13,fill:theme.fill,stroke:theme.stroke,'stroke-width':1.2}));
        group.append(svg('rect',{x:0,y:10,width:4,height:h-20,rx:2,fill:theme.accent}));
        const kindText=String(node.kind || 'Symbol');
        const kindWidth=clamp(kindText.length*7.1+21,65,150);
        group.append(svg('rect',{x:15,y:12,width:kindWidth,height:24,rx:7,fill:theme.accent,'fill-opacity':.13}));
        group.append(label(kindText,{x:25,y:28,'font-family':'Tahoma, sans-serif','font-size':11,'font-weight':700,fill:theme.accent}));
        if(node.referenceCount){
            group.append(label(`${node.referenceCount} ref${node.referenceCount===1?'':'s'}`,
                {x:w-42,y:27,'text-anchor':'end','font-family':'Tahoma,sans-serif','font-size':10,
                    'font-weight':700,fill:'#527384'}));
        }
        box.title.forEach((line,i)=>group.append(label(line,{x:18,y:box.titleStart+i*19,
            'font-family':'Consolas,Tahoma,monospace','font-size':13,'font-weight':700,fill:'#183e4d'})));
        box.subtitle.forEach((line,i)=>group.append(label(line,{x:18,y:box.subtitleStart+i*15,
            'font-family':'Tahoma,Consolas,sans-serif','font-size':10.5,fill:'#537485'})));
        if(node.isCycle){
            group.append(label('↻', {x:w-18,y:h-12,'text-anchor':'end','font-size':13,fill:'#ac7b2e'}));
        }
        const childCount=(state.children.get(node.id)||[]).length;
        if(childCount || node.expandSymbolId) {
            const toggle=svg('g',{class:'graph-expand','data-expand-id':node.id,role:'button',tabindex:'0',
                'aria-label':state.expanded.has(node.id)?'بستن زیرمجموعه':'نمایش ارجاعات این نماد'});
            toggle.style.cursor='pointer';
            const toggleX=w-32;
            toggle.append(svg('rect',{x:toggleX,y:9,width:25,height:25,rx:7,fill:'#fff',stroke:theme.stroke,'stroke-width':1.25}));
            toggle.append(svg('path',{d:`M${toggleX+7} 21.5h11`,stroke:theme.accent,'stroke-width':2,'stroke-linecap':'round'}));
            if(!state.expanded.has(node.id))
                toggle.append(svg('path',{d:`M${toggleX+12.5} 16v11`,stroke:theme.accent,'stroke-width':2,'stroke-linecap':'round'}));
            group.append(toggle);
        }
        group.addEventListener('mouseenter',ev=>tooltip(state,node,ev));
        group.addEventListener('mouseleave',()=>{state.tooltip.style.display='none';});
        group.addEventListener('focusout',()=>{state.tooltip.style.display='none';});
        group.addEventListener('mousemove',ev=>{if(!ev.target.closest('[data-expand-id]'))tooltip(state,node,ev);});
        nodeLayer.append(group);state.groups.set(node.id,group);
    }
    state.viewport.append(edgeLayer,nodeLayer);links(state);paint(state);
}
function rebuild(state,refit=true) {arrange(state);draw(state);if(refit)fit(state);else transform(state);}
function zoomAt(state,factor,x,y) {
    const next=clamp(state.scale*factor,.12,3.7),ratio=next/state.scale;
    state.panX=x-(x-state.panX)*ratio;state.panY=y-(y-state.panY)*ratio;
    state.scale=next;transform(state);
}
async function toggle(state,id) {
    const node=state.byId.get(id);
    if(state.expanded.has(id)) {
        state.expanded.delete(id);
        state.dotnet.invokeMethodAsync('CollapseNode',id).catch(()=>{});
    } else state.expanded.add(id);
    state.highlight=null;rebuild(state);
    if(node?.expandSymbolId && !state.requested.has(id)) {
        state.requested.add(id);
        try {
            const accepted=await state.dotnet.invokeMethodAsync('ExpandNode',id);
            if(!accepted)state.requested.delete(id);
        } catch (error) {
            state.requested.delete(id);
            // CENOPS displays service errors in its page-level alert.
            console.warn('Reference expansion failed:',error);
        }
    }
}
function select(state,id,additive=false) {
    if(additive) {
        if(state.selectedIds.includes(id))state.selectedIds=state.selectedIds.filter(x=>x!==id);
        else state.selectedIds.push(id);
    } else state.selectedIds=[id];
    state.selected=state.selectedIds.at(-1)||null;
    state.highlight=null;paint(state);
    state.dotnet.invokeMethodAsync('SelectionChanged',state.selectedIds).catch(()=>{});
    if(state.selected)state.dotnet.invokeMethodAsync('SelectNode',state.selected).catch(()=>{});
    else state.dotnet.invokeMethodAsync('DismissDetails').catch(()=>{});
}
export function mount(container,nodes,edges,dotnet) {
    unmount(container);
    const stage=container.querySelector('.source-graph-stage');
    const viewport=container.querySelector('.source-graph-viewport');
    const tooltip=container.querySelector('.source-graph-tooltip');
    if(!stage||!viewport||!tooltip)return;
    const state={container,stage,viewport,tooltip,dotnet,nodes:nodes||[],edges:edges||[],
        byId:new Map((nodes||[]).map(n=>[n.id,n])),dimensions:new Map(),requested:new Set(),measure:document.createElement('canvas').getContext('2d'),
        abort:new AbortController(),selected:null,selectedIds:[],highlight:null,isolate:null,drag:null,scale:1,panX:0,panY:0,positions:new Map(),groups:new Map(),paths:[]};
    ({children:state.children,parent:state.parent}=hierarchy(state.nodes,state.edges));
    state.expanded=new Set(state.nodes.filter(n=>n.level===0).map(n=>n.id));
    instances.set(container,state);rebuild(state);
    state.detachModifiers = attachGraphModifierBadges(container);
    const opt={signal:state.abort.signal};
    stage.addEventListener('wheel',ev=>{
        ev.preventDefault();
        if(ev.ctrlKey||ev.metaKey){const r=stage.getBoundingClientRect();zoomAt(state,Math.exp(-ev.deltaY*.0011),ev.clientX-r.left,ev.clientY-r.top);}
        else if(ev.shiftKey)state.panX-=ev.deltaY+ev.deltaX;
        else state.panY-=ev.deltaY;
        transform(state);
    },{...opt,passive:false});
    stage.addEventListener('pointerdown',ev=>{
        if(ev.button!==0)return;
        const exp=ev.target.closest('[data-expand-id]');
        if(exp){ev.stopPropagation();toggle(state,exp.getAttribute('data-expand-id'));return;}
        const target=ev.target.closest('[data-node-id]');
        const id=target?.getAttribute('data-node-id');
        state.drag={id,x:ev.clientX,y:ev.clientY,moved:false,panX:state.panX,panY:state.panY,
            pos:id?{...state.positions.get(id)}:null,additive:ev.ctrlKey||ev.metaKey};
        stage.setPointerCapture(ev.pointerId);state.tooltip.style.display='none';
    },opt);
    stage.addEventListener('pointermove',ev=>{
        const d=state.drag;if(!d)return;
        const dx=ev.clientX-d.x,dy=ev.clientY-d.y;
        if(Math.abs(dx)+Math.abs(dy)>4)d.moved=true;
        if(!d.moved)return;
        if(d.id&&d.pos){state.positions.set(d.id,{x:d.pos.x+dx/state.scale,y:d.pos.y+dy/state.scale});
            const group=state.groups.get(d.id),p=state.positions.get(d.id);if(group)group.setAttribute('transform',`translate(${p.x},${p.y})`);
            links(state);
        }else{state.panX=d.panX+dx;state.panY=d.panY+dy;transform(state);}
    },opt);
    const end=ev=>{
        const d=state.drag;if(!d)return;state.drag=null;
        if(stage.hasPointerCapture(ev.pointerId))stage.releasePointerCapture(ev.pointerId);
        if(d.id&&!d.moved)select(state,d.id,d.additive);
        else if(!d.id&&!d.moved) {
            state.selected=null;state.selectedIds=[];state.highlight=null;paint(state);
            state.dotnet.invokeMethodAsync('SelectionChanged',[]).catch(()=>{});
            state.dotnet.invokeMethodAsync('DismissDetails').catch(()=>{});
        }
    };
    stage.addEventListener('pointerup',end,opt);stage.addEventListener('pointercancel',end,opt);
    stage.addEventListener('keydown',ev=>{
        if(ev.key!=='Enter'&&ev.key!==' ')return;
        const exp=ev.target.closest('[data-expand-id]');
        const node=ev.target.closest('[data-node-id]');
        if(exp){ev.preventDefault();toggle(state,exp.getAttribute('data-expand-id'));}
        else if(node){ev.preventDefault();select(state,node.getAttribute('data-node-id'),ev.ctrlKey||ev.metaKey);}
    },opt);
    const resize=()=>fit(state);window.addEventListener('resize',resize,opt);
    document.addEventListener('fullscreenchange',resize,opt);
}
export function update(container,nodes,edges) {
    const state=instances.get(container); if(!state)return;
    state.nodes=nodes||[];state.edges=edges||[];
    state.byId=new Map(state.nodes.map(n=>[n.id,n]));state.dimensions.clear();
    ({children:state.children,parent:state.parent}=hierarchy(state.nodes,state.edges));
    state.expanded=new Set([...state.expanded].filter(id=>state.byId.has(id)));
    state.requested=new Set([...state.requested].filter(id=>state.byId.has(id)));
    state.selectedIds=state.selectedIds.filter(id=>state.byId.has(id));
    if(state.selected&&!state.byId.has(state.selected))state.selected=state.selectedIds.at(-1)||null;
    state.highlight=null;
    state.isolate=null; // Expanded references restore visibility of newly discovered consumers.
    rebuild(state,false); // Preserve current viewport/zoom during on-demand expansion.
}
// View cleanup must not collapse expanded nodes or undo the user's isolation.
export function reset(container){const s=instances.get(container);if(!s)return;
    s.highlight=null;rebuild(s);
}
export function clearSelection(container){const s=instances.get(container);if(!s)return;s.selected=null;s.selectedIds=[];s.highlight=null;paint(s);s.dotnet.invokeMethodAsync('SelectionChanged',[]).catch(()=>{});}
export function zoom(container,factor){const s=instances.get(container);if(s)zoomAt(s,factor,s.stage.clientWidth/2,s.stage.clientHeight/2);}
export async function fullscreen(container){if(document.fullscreenElement===container)await document.exitFullscreen();else if(container?.requestFullscreen)await container.requestFullscreen();}
export async function exitFullscreen(container){if(document.fullscreenElement===container)await document.exitFullscreen();}
export function highlightPath(container,isolate=false) {
    const s=instances.get(container);if(!s||s.selectedIds.length!==2)return false;
    const route=allRouteEdges([...s.visible],graphLinks(s),s.selectedIds[0],s.selectedIds[1]);
    if(!route)return false;
    if(isolate){s.isolate=route;s.highlight=null;rebuild(s,true);}
    else{s.highlight=route;paint(s);}
    return true;
}
export function focusBranch(container,isolate=false){
    const s=instances.get(container);if(!s||s.selectedIds.length!==1)return;
    const near=nearEdges(graphLinks(s),s.selectedIds.at(-1));
    if(isolate){s.isolate=near;s.highlight=null;rebuild(s,true);}
    else{s.highlight=near;paint(s);}
}
export function unmount(container){const s=instances.get(container);if(!s)return;
    s.abort.abort();s.detachModifiers?.();s.tooltip.style.display='none';instances.delete(container);
}
