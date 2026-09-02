const API="http://localhost:6051/api";
const t=(await (await fetch(`${API}/auth/login`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({login:'owner@sivayaan',password:'Owner@123'})})).json()).data.accessToken;
const g=async p=>(await (await fetch(`${API}${p}`,{headers:{Authorization:`Bearer ${t}`}})).json()).data;
const reqs=(await g('/material-requests?pageSize=50')).items;
const exp=(await g('/expenses?pageSize=300')).items;
console.log('Material requests — date on the form vs date the cost landed:\n');
console.log('  request date   txn                       cost rows dated');
const byRef=new Map();
for(const e of exp){ if(!/Consumption/.test(e.description??''))continue; const k=(e.description.match(/MATREQ\S+/)||[])[0]; (byRef.get(k)??byRef.set(k,[]).get(k)).push(e.date.slice(0,10)); }
for(const r of reqs.slice(0,6)){ const d=[...new Set(byRef.get(r.txnNumber)??[])]; console.log(`  ${r.date.slice(0,10)}     ${r.txnNumber.padEnd(22)}  ${d.join(', ')||'—'}`); }
const today=new Date().toISOString().slice(0,10);
const material=exp.filter(e=>/Consumption/.test(e.description??''));
console.log(`\n  ${material.length} material cost rows; ${material.filter(e=>e.date.slice(0,10)===today).length} of them dated today (${today})`);
