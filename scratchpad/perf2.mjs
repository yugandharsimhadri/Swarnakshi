import http from "node:http";
const agent = new http.Agent({ keepAlive: true, maxSockets: 1 });
const req = (path, headers = {}) => new Promise((res, rej) => {
  const r = http.request({ host: "localhost", port: 6051, path, headers, agent }, (x) => {
    let b = ""; x.on("data", (c) => (b += c)); x.on("end", () => res({ status: x.statusCode, body: b }));
  });
  r.on("error", rej); r.end();
});
const post = (path, obj) => new Promise((res, rej) => {
  const d = JSON.stringify(obj);
  const r = http.request({ host: "localhost", port: 6051, path, method: "POST", agent,
    headers: { "Content-Type": "application/json", "Content-Length": Buffer.byteLength(d) } }, (x) => {
    let b = ""; x.on("data", (c) => (b += c)); x.on("end", () => res(JSON.parse(b)));
  });
  r.on("error", rej); r.write(d); r.end();
});

const login = await post("/api/auth/login", { login: "owner@scopsbook", password: "Owner@123" });
const H = { Authorization: `Bearer ${login.data.accessToken}` };
const j = async (p) => JSON.parse((await req(p, H)).body).data;
const villa = (await j("/api/projects?pageSize=100")).items.find(p => p.name === "Villa 101");
const site  = (await j("/api/sites?pageSize=50")).items[0];

const endpoints = [
  ["health (baseline)",      "/health"],
  ["dashboard",              "/api/dashboard"],
  ["projects list",          "/api/projects?pageSize=50"],
  ["project summary",        `/api/projects/${villa.id}/summary`],
  ["project expenses",       `/api/expenses?projectId=${villa.id}&pageSize=100`],
  ["materials (500)",        "/api/materials?pageSize=500&active=true"],
  ["inventory txns (100)",   `/api/inventory/transactions?siteId=${site.id}&pageSize=100`],
  ["purchases list",         "/api/purchases?pageSize=50"],
  ["report: stock",          "/api/reports/inventory/stock"],
  ["report: purchase reg",   "/api/reports/inventory/purchase-register"],
  ["report: consumption",    "/api/reports/inventory/consumption"],
  ["report: project cost",   "/api/reports/project/cost-summary"],
  ["report: company",        "/api/reports/company/summary"],
  ["report: site summary",   "/api/reports/site/summary"],
  ["report: profitability",  "/api/reports/project/profitability"],
  ["report: budget burn",    "/api/reports/project/budget-burn"],
];
const N = 50, pct = (a,p) => a[Math.min(a.length-1, Math.floor(a.length*p))];
const rows = [];
for (const [name, path] of endpoints) {
  for (let i = 0; i < 8; i++) await req(path, H);
  const t = []; let kb = 0, bad = 0;
  for (let i = 0; i < N; i++) {
    const s = performance.now(); const r = await req(path, H); t.push(performance.now() - s);
    kb = r.body.length / 1024; if (r.status >= 400) bad++;
  }
  t.sort((a,b)=>a-b);
  rows.push({ name, p50: pct(t,.5), p95: pct(t,.95), max: t[t.length-1], kb, bad });
}
rows.sort((a,b)=>b.p95-a.p95);
const f = n => n.toFixed(2).padStart(8);
console.log("\n" + "endpoint".padEnd(24) + "p50 ms".padStart(8) + "p95 ms".padStart(9) + "max ms".padStart(9) + "resp KB".padStart(10));
console.log("-".repeat(61));
for (const r of rows) console.log(r.name.padEnd(24) + f(r.p50) + f(r.p95).padStart(9) + f(r.max).padStart(9) + r.kb.toFixed(1).padStart(10) + (r.bad ? `  ${r.bad} errors` : ""));
