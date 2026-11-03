// Run: node --experimental-default-type=module tests/GraphTraversalRegression/test_routes.mjs
import { strict as assert } from 'node:assert';
import { allRouteEdges, nearEdges } from '../../src/NTSW_CENOPS.WebUI/wwwroot/js/graph-paths.js';

function brute(vertices, edges, start, end) {
  const used=new Set([start]), union=new Set(), route=[];
  function visit(current) {
    if(current===end) { for(const id of route)union.add(id);return; }
    for(const edge of edges) {
      if(edge.from!==current&&edge.to!==current)continue;
      const next=edge.from===current?edge.to:edge.from;
      if(used.has(next))continue;
      used.add(next);route.push(edge.id);visit(next);
      route.pop();used.delete(next);
    }
  }
  visit(start);
  return union.size?[...union].sort((a,b)=>a-b):null;
}
function expected(vertices,edges,start,end) {
  const actual=allRouteEdges(vertices,edges,start,end);
  const ref=brute(vertices,edges,start,end);
  assert.deepEqual(actual?[...actual.edges].sort((a,b)=>a-b):null,ref);
  if(actual) assert(actual.nodes.has(start)&&actual.nodes.has(end));
}
const p=[1,2,3,4,5];
const edges=[{id:11,from:1,to:2},{id:12,from:2,to:3},
 {id:13,from:1,to:3},{id:14,from:3,to:4},{id:15,from:1,to:2}];
expected(p,edges,1,4);
assert.deepEqual([...nearEdges(edges,3).edges].sort((a,b)=>a-b),[12,13,14]);
assert.equal(allRouteEdges(p,edges,1,5),null);
assert.equal(allRouteEdges(p,edges,1,1),null);
// Deterministic randomized regression against exhaustive simple-path enumeration.
let seed=0x1A2B3C4D;
const rand=()=>{seed^=seed<<13;seed^=seed>>>17;seed^=seed<<5;return (seed>>>0)/4294967296;};
let checks=0;
for(let trial=0;trial<450;trial++) {
  const n=2+Math.floor(rand()*6), nodes=Array.from({length:n},(_,i)=>i);
  const relations=[];
  for(let i=0;i<13;i++)if(rand()<.57){
    relations.push({id:i+1,from:Math.floor(rand()*n),to:Math.floor(rand()*n)});
  }
  for(let a=0;a<n;a++)for(let z=a+1;z<n;z++){
    expected(nodes,relations,a,z);checks++;
  }
}
console.log('PASS:',checks,'route unions agree with exhaustive paths; parallel FKs, cycles, self loops and isolated nodes');
