// Shared graph theory for the CENOPS renderers. Undirected *simple* routes: FK links
// can be traversed in either direction. Block-cut decomposition finds the union
// of ALL simple routes without enumerating an exponential number of paths.
// Edge IDs must be unique per relationship; parallel FKs remain separate edges.
export function allRouteEdges(vertices, links, start, end) {
  const ids = new Set(vertices);
  if (start === end || !ids.has(start) || !ids.has(end)) return null;
  const adjacency = new Map([...ids].map(id => [id, []]));
  const normalized = [];
  for (const link of links) {
    if (!ids.has(link.from) || !ids.has(link.to)) continue;
    const idx = normalized.length;
    normalized.push(link);
    adjacency.get(link.from).push(idx);
    if (link.from !== link.to) adjacency.get(link.to).push(idx);
  }
  // Tarjan biconnected edge components. Self loops cannot connect distinct vertices.
  let time = 0;
  const discovered = new Map(), low = new Map(), stack = [], blocks = [];
  function visit(u, parentEdge = -1) {
    discovered.set(u, ++time); low.set(u, time);
    for (const ei of adjacency.get(u)) {
      const edge = normalized[ei];
      const v = edge.from === u ? edge.to : edge.from;
      if (v === u || ei === parentEdge) continue;
      if (!discovered.has(v)) {
        stack.push(ei); visit(v, ei);
        low.set(u, Math.min(low.get(u), low.get(v)));
        if (low.get(v) >= discovered.get(u)) {
          const component = [];
          let popped;
          do { popped = stack.pop(); component.push(popped); } while (popped !== ei);
          blocks.push(component);
        }
      } else if (discovered.get(v) < discovered.get(u)) {
        stack.push(ei); low.set(u, Math.min(low.get(u), discovered.get(v)));
      }
    }
  }
  for (const id of ids) if (!discovered.has(id)) visit(id);
  // Bipartite block-cut forest: a vertex is linked to every block containing it.
  const tree = new Map();
  const connect = (a, b) => {
    if (!tree.has(a)) tree.set(a, []);
    if (!tree.has(b)) tree.set(b, []);
    tree.get(a).push(b); tree.get(b).push(a);
  };
  const blockTokens = blocks.map((_,i)=>Symbol('block-'+i));
  blocks.forEach((component, bi) => {
    const vset = new Set();
    for (const ei of component) { vset.add(normalized[ei].from); vset.add(normalized[ei].to); }
    for (const id of vset) connect(id, blockTokens[bi]);
  });
  // Using symbols for block nodes guarantees no collision with actual node IDs.
  const queue = [start], previous = new Map([[start, null]]);
  for (let at = 0; at < queue.length && !previous.has(end); ++at) {
    for (const neighbor of tree.get(queue[at]) || []) {
      if (!previous.has(neighbor)) { previous.set(neighbor, queue[at]); queue.push(neighbor); }
    }
  }
  if (!previous.has(end)) return null;
  const selectedBlocks = new Set();
  for (let item = end; item !== null; item = previous.get(item)) {
    if (typeof item === 'symbol') selectedBlocks.add(item);
  }
  const routeNodes = new Set([start, end]), routeEdges = new Set();
  blocks.forEach((component, bi) => {
    if (!selectedBlocks.has(blockTokens[bi])) return;
    for (const ei of component) {
      const e = normalized[ei]; routeEdges.add(e.id);
      routeNodes.add(e.from); routeNodes.add(e.to);
    }
  });
  return {nodes: routeNodes, edges: routeEdges};
}

export function nearEdges(links, id) {
  const nodes = new Set([id]), edges = new Set();
  for (const edge of links) {
    if (edge.from !== id && edge.to !== id) continue;
    nodes.add(edge.from); nodes.add(edge.to); edges.add(edge.id);
  }
  return {nodes, edges};
}
