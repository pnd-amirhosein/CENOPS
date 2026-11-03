/**
 * SQL Server query previews for Database Inspector.
 * Pure metadata-to-text conversion. Never executes SQL, reads credentials or needs a DOM.
 * One SELECT is emitted for each connected component (or table in SELECT mode).
 */
const quote = identifier => `[${String(identifier).replaceAll(']', ']]')}]`;
const qualified = table => `${quote(table.schema)}.${quote(table.name)}`;
const byId = (a, b) => a.id - b.id;

function projection(design, alias) {
  if (!design.columns?.length) throw new Error(`Table ${qualified(design.table)} has no available columns.`);
  return design.columns
    .slice().sort((a, b) => a.ordinal - b.ordinal)
    .map(column => `    ${quote(alias)}.${quote(column.name)} AS ${quote(`${alias}_${column.name}`)}`);
}

function eligibleRelationships(designs, relations) {
  const ids = new Set(designs.map(design => design.table.id));
  return relations.filter(fk =>
    ids.has(fk.child?.id) && ids.has(fk.parent?.id) && fk.child.id !== fk.parent.id &&
    Array.isArray(fk.columns) && fk.columns.length > 0 &&
    fk.columns.every(pair => pair.childColumn && pair.parentColumn))
    .sort(byId);
}

/**
 * Plan the smallest FK-hop routes for each pair connection (undirected FK traversal).
 * Intermediate, unselected tables are valid JOIN bridges. If several tables are
 * selected, a deterministic multi-source BFS attaches the next closest selected
 * table to the growing tree; this prevents redundant joins and is a bounded
 * Steiner-tree heuristic (globally optimal only for a pair of terminals).
 * No database rows are read and the graph viewport is not involved.
 * @returns {{tableIds: number[], relationshipIds: number[]}}
 */
export function planJoinPaths(selectedTableIds, relations) {
  const terminals = [...new Set(selectedTableIds)].sort((a, b) => a - b);
  if (!terminals.length) return { tableIds: [], relationshipIds: [] };
  const adjacency = new Map();
  for (const fk of [...relations].sort(byId)) {
    const child = fk.child?.id, parent = fk.parent?.id;
    if (!Number.isInteger(child) || !Number.isInteger(parent) || child === parent ||
        !Array.isArray(fk.columns) || !fk.columns.length ||
        fk.columns.some(pair => !pair.childColumn || !pair.parentColumn)) continue;
    if (!adjacency.has(child)) adjacency.set(child, []);
    if (!adjacency.has(parent)) adjacency.set(parent, []);
    adjacency.get(child).push({ next: parent, relationId: fk.id });
    adjacency.get(parent).push({ next: child, relationId: fk.id });
  }
  for (const neighbors of adjacency.values())
    neighbors.sort((a, b) => a.relationId - b.relationId || a.next - b.next);

  const allNodes = new Set(terminals);
  const selectedRelations = new Set();
  const remaining = new Set(terminals);
  while (remaining.size) {
    // Each fresh root starts a separate query if no other selected table is reachable.
    const root = Math.min(...remaining);
    remaining.delete(root);
    const tree = new Set([root]);
    while (remaining.size) {
      // One BFS from all nodes already in the tree ensures the next bridge is
      // the shortest possible path to this tree, not simply to the first table.
      const queue = [...tree].sort((a, b) => a - b);
      const previous = new Map(queue.map(id => [id, null]));
      let target = null;
      for (let head = 0; head < queue.length && target === null; head++) {
        const node = queue[head];
        if (remaining.has(node)) { target = node; break; }
        for (const { next, relationId } of adjacency.get(node) ?? []) {
          if (previous.has(next)) continue;
          previous.set(next, { from: node, relationId });
          queue.push(next);
        }
      }
      if (target === null) break;
      remaining.delete(target);
      let current = target;
      while (!tree.has(current)) {
        const step = previous.get(current);
        if (!step) throw new Error('Invalid relationship path.');
        selectedRelations.add(step.relationId);
        allNodes.add(current);
        tree.add(current);
        current = step.from;
      }
    }
  }
  return {
    tableIds: [...allNodes].sort((a, b) => a - b),
    relationshipIds: [...selectedRelations].sort((a, b) => a - b)
  };
}

function buildIndependent(designs) {
  return designs.map(design => {
    const alias = 't1';
    return `SELECT\n${projection(design, alias).join(',\n')}\nFROM ${qualified(design.table)} AS ${quote(alias)};`;
  }).join('\n\n');
}

function buildJoined(designs, relations, selectedTableIds) {
  const projectedIds = new Set(selectedTableIds);
  const designsById = new Map(designs.map(design => [design.table.id, design]));
  const eligible = eligibleRelationships(designs, relations);
  const adjacency = new Map(designs.map(design => [design.table.id, []]));
  for (const fk of eligible) {
    adjacency.get(fk.child.id).push({ id: fk.parent.id, fk });
    adjacency.get(fk.parent.id).push({ id: fk.child.id, fk });
  }
  const visited = new Set();
  const statements = [];

  // A spanning tree joins every connected selected table exactly once, avoiding
  // accidental cartesian products and duplicate table JOINs in cycles.
  for (const first of designs) {
    const rootId = first.table.id;
    if (visited.has(rootId)) continue;
    visited.add(rootId);
    const aliases = new Map([[rootId, 't1']]);
    const joinClauses = [];
    const members = [rootId];
    const queue = [rootId];
    for (let index = 0; index < queue.length; index++) {
      const current = queue[index];
      for (const { id, fk } of adjacency.get(current)) {
        if (visited.has(id)) continue;
        visited.add(id);
        queue.push(id);
        members.push(id);
        const alias = `t${members.length}`;
        aliases.set(id, alias);
        const childAlias = aliases.get(fk.child.id);
        const parentAlias = aliases.get(fk.parent.id);
        const conditions = fk.columns.slice().sort((a, b) => a.ordinal - b.ordinal)
          .map(pair => `${quote(childAlias)}.${quote(pair.childColumn)} = ${quote(parentAlias)}.${quote(pair.parentColumn)}`);
        joinClauses.push(`INNER JOIN ${qualified(designsById.get(id).table)} AS ${quote(alias)}\n    ON ${conditions.join('\n   AND ')}`);
      }
    }
    // Bridge tables participate in JOIN/ON only: project selected tables explicitly.
    const select = members.filter(id => projectedIds.has(id))
      .flatMap(id => projection(designsById.get(id), aliases.get(id)));
    statements.push(`SELECT\n${select.join(',\n')}\nFROM ${qualified(first.table)} AS [t1]${joinClauses.length ? '\n' + joinClauses.join('\n') : ''};`);
  }
  return statements.join('\n\n');
}

/**
 * @param {object[]} designs SQL Server table designs (table + ordered columns).
 * @param {object[]} relations SQL Server FK definitions including composite mappings.
 * @param {'select'|'join'} mode Output style.
 * @param {number[]} selectedTableIds Original user selection; intermediate JOIN bridges are not projected.
 * @returns {string} Plain, non-executed T-SQL text.
 */
export function buildSqlQueries(designs, relations, mode, selectedTableIds = designs.map(d => d.table.id)) {
  if (!Array.isArray(designs) || !designs.length) throw new Error('Select at least one table.');
  if (new Set(designs.map(design => design.table.id)).size !== designs.length)
    throw new Error('Duplicate table IDs are not allowed.');
  const selected = new Set(selectedTableIds);
  if (!selected.size || [...selected].some(id => !designs.some(d => d.table.id === id)))
    throw new Error('Selected table metadata is missing.');
  if (mode === 'select') return buildIndependent(designs.filter(d => selected.has(d.table.id)));
  if (mode === 'join') return buildJoined(designs, Array.isArray(relations) ? relations : [], selected);
  throw new Error('Unsupported export mode.');
}
