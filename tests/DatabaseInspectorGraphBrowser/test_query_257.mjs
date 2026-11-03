// Run: node --experimental-default-type=module --test tests/DatabaseInspectorGraphBrowser/test_query_257.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import {buildSqlQueries, planJoinPaths} from '../../src/NTSW_CENOPS.WebUI/wwwroot/js/database-query-export.js';
const table = id => ({table:{id,schema:'dbo',name:`Table${id}`},columns:[
  {name:'Id',ordinal:1},{name:'ParentId',ordinal:2},{name:`Extra${id}`,ordinal:3}]});
const fk = (id, child, parent, mappings=[['ParentId','Id']]) => ({id,child:{id:child},parent:{id:parent},
  columns:mappings.map(([childColumn,parentColumn],index)=>({childColumn,parentColumn,ordinal:index+1}))});
const emit = (selected, catalog) => {
  const plan = planJoinPaths(selected,catalog);
  const relations = catalog.filter(r=>plan.relationshipIds.includes(r.id));
  return {plan,sql:buildSqlQueries(plan.tableIds.map(table),relations,'join',selected)};
};

test('connects two selected tables across three unselected bridge joins',()=>{
  const {plan,sql}=emit([1,5],[fk(101,1,2),fk(102,2,3),fk(103,3,4),fk(104,4,5)]);
  assert.deepEqual(plan.tableIds,[1,2,3,4,5]);
  assert.deepEqual(plan.relationshipIds,[101,102,103,104]);
  assert.equal((sql.match(/INNER JOIN/g)||[]).length,4);
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,1);
  assert.match(sql,/\[Extra1\]/);
  assert.match(sql,/\[Extra5\]/);
  assert.doesNotMatch(sql,/\[Extra2\]|\[Extra3\]|\[Extra4\]/);
  assert.doesNotMatch(sql,/SELECT\s+\*/);
});

test('prefers two-hop route to four-hop alternative',()=>{
  const r=[fk(1,1,2),fk(2,2,3),fk(3,3,4),fk(4,4,5),fk(5,1,9),fk(6,9,5)];
  const {plan,sql}=emit([1,5],r);
  assert.deepEqual(plan.relationshipIds,[5,6]);
  assert.deepEqual(plan.tableIds,[1,5,9]);
  assert.equal((sql.match(/INNER JOIN/g)||[]).length,2);
});

test('equal-hop alternative paths use stable FK-ID tie-breaking',()=>{
  const r=[fk(20,1,2),fk(21,2,4),fk(5,1,3),fk(6,3,4)];
  const plan=planJoinPaths([4,1],r);
  assert.deepEqual(plan.relationshipIds,[5,6]);
  assert.deepEqual(plan.tableIds,[1,3,4]);
});

test('multi-terminal path attaches each selected table once; disconnected groups are separate statements',()=>{
  const r=[fk(10,1,2),fk(11,2,3),fk(12,3,4),fk(13,4,5),fk(14,5,6),
    fk(30,10,11),fk(31,11,12),fk(32,21,22)];
  const {plan,sql}=emit([1,4,6,10,12,90],r);
  assert.deepEqual(plan.tableIds,[1,2,3,4,5,6,10,11,12,90]);
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,3);
  assert.equal((sql.match(/INNER JOIN/g)||[]).length,7);
  assert.match(sql,/FROM \[dbo\]\.\[Table90\] AS \[t1\];/);
});

test('composite FK path retains every ON predicate, with explicit projections',()=>{
  const r=[fk(1,1,2,[['ParentId','Id'],['Id','ParentId']]),fk(2,2,3)];
  const {sql}=emit([1,3],r);
  assert.match(sql,/\[t1\]\.\[ParentId\] = \[t2\]\.\[Id\]\n   AND \[t1\]\.\[Id\] = \[t2\]\.\[ParentId\]/);
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,1);
});

test('cycles, parallel FK and orphan selected tables do not create repeats',()=>{
  const r=[fk(11,1,2),fk(12,2,3),fk(13,3,1),fk(14,1,2),fk(19,20,21)];
  const {plan,sql}=emit([1,3,99],r);
  assert.equal(plan.relationshipIds.length,1); // Direct link is shorter than the two-hop route
  assert.deepEqual(plan.relationshipIds,[13]);
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,2);
  assert.equal((sql.match(/INNER JOIN/g)||[]).length,1);
});

test('empty, malformed or self relations do not create phantom paths',()=>{
  assert.deepEqual(planJoinPaths([],[]),{tableIds:[],relationshipIds:[]});
  const {plan,sql}=emit([1,2],[{id:30,child:{id:1},parent:{id:2},columns:[]},fk(32,1,1)]);
  assert.deepEqual(plan.relationshipIds,[]);
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,2);
});
