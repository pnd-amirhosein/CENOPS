// Run: node --test tests/DatabaseInspectorGraphBrowser/test_query_256.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import {buildSqlQueries} from '../../src/NTSW_CENOPS.WebUI/wwwroot/js/database-query-export.js';

const design = (id, name, columns=['Id','Name'], schema='dbo') => ({
  table:{id,name,schema}, columns:columns.map((name,ordinal)=>({name,ordinal:ordinal+1}))
});
const fk = (id, child, parent, mappings=[['ParentId','Id']]) => ({
  id,child:{id:child},parent:{id:parent}, columns:mappings.map(([childColumn,parentColumn],i)=>({childColumn,parentColumn,ordinal:i+1}))
});
const tables = [design(1,'Orders',['Id','CustomerId','StoreId']),design(2,'Customers',['Id','Name']),
  design(3,'Stores',['Id','Name']),design(4,'People',['Id','Name']),design(5,'Addresses',['Id','PersonId']),
  design(6,'Settings',['Id','Value'])];
const relations = [fk(101,1,2,[['CustomerId','Id']]),fk(102,1,3,[['StoreId','Id']]),fk(105,5,4,[['PersonId','Id']])];

test('independent SELECTs have explicit columns and escaped SQL aliases',()=>{
  const sql=buildSqlQueries(tables.slice(0,2),relations,'select');
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,2);
  assert.equal((sql.match(/\bINNER JOIN\b/g)||[]).length,0);
  assert.match(sql,/\[t1\]\.\[CustomerId\] AS \[t1_CustomerId\]/);
  assert.doesNotMatch(sql,/SELECT\s+\*/);
  assert.match(sql,/FROM \[dbo\]\.\[Customers\] AS \[t1\]/);
});

test('connected components become independent join statements; isolated nodes use SELECT',()=>{
  const sql=buildSqlQueries(tables,relations,'join');
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,3);
  assert.equal((sql.match(/\bINNER JOIN\b/g)||[]).length,3);
  assert.match(sql,/\[t1\]\.\[CustomerId\] = \[t2\]\.\[Id\]/);
  assert.match(sql,/\[t1\]\.\[StoreId\] = \[t3\]\.\[Id\]/);
  assert.match(sql,/FROM \[dbo\]\.\[People\] AS \[t1\]\nINNER JOIN \[dbo\]\.\[Addresses\]/);
  assert.match(sql,/FROM \[dbo\]\.\[Settings\] AS \[t1\];/);
  assert.equal(sql.split(';').filter(x=>x.trim()).length,3);
});

test('composite FK mapping uses AND on all columns, sorted by ordinal',()=>{
  const t=[design(10,'Sales',['Location','Code']),design(11,'Locations',['Location','Code'])];
  const sql=buildSqlQueries(t,[fk(99,10,11,[['Code','Code'],['Location','Location']])],'join');
  assert.match(sql,/\[t1\]\.\[Code\] = \[t2\]\.\[Code\]\n   AND \[t1\]\.\[Location\] = \[t2\]\.\[Location\]/);
});

test('unselected bridging tables do not create a phantom join',()=>{
  const sql=buildSqlQueries([tables[0],tables[2]],[fk(1,1,2),fk(2,2,3)],'join');
  assert.equal((sql.match(/\bINNER JOIN\b/g)||[]).length,0);
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,2);
});

test('cycles and duplicate foreign keys never repeat a joined table',()=>{
  const sql=buildSqlQueries(tables.slice(0,3),[...relations,fk(300,3,2),fk(301,1,2)],'join');
  assert.equal((sql.match(/\bSELECT\b/g)||[]).length,1);
  assert.equal((sql.match(/\bINNER JOIN\b/g)||[]).length,2);
});

test('SQL Server special identifier bracket escaping',()=>{
  const sql=buildSqlQueries([design(7,'Odd]Table',['a]b','Qty'], 'a]b')],[],'join');
  assert.match(sql,/FROM \[a\]\]b\]\.\[Odd\]\]Table\]/);
  assert.match(sql,/\[t1\]\.\[a\]\]b\] AS \[t1_a\]\]b\]/);
});

test('invalid arguments and missing columns are rejected instead of SELECT *',()=>{
  assert.throws(()=>buildSqlQueries([],[],'select'),/Select at least one/);
  assert.throws(()=>buildSqlQueries([design(1,'A'),design(1,'A')],[],'join'),/Duplicate/);
  assert.throws(()=>buildSqlQueries([design(1,'A',[])],[],'select'),/no available columns/);
  assert.throws(()=>buildSqlQueries([design(1,'A')],[],'drop'),/Unsupported/);
});
