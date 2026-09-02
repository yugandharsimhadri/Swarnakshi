/** Interrogates the seeded book of work for things that do not add up. */
const API = "http://localhost:6051/api";
let token;

async function call(m, p, b) {
  const r = await fetch(`${API}${p}`, {
    method: m,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: b === undefined ? undefined : JSON.stringify(b),
  });
  const j = await r.json().catch(() => null);
  if (!r.ok) throw new Error(`${m} ${p} → ${r.status}: ${j?.message ?? ""}`);
  return j?.data ?? j;
}
const get = (p) => call("GET", p);
const L = (n) => {
  const v = Number(n ?? 0);
  if (Math.abs(v) >= 1e7) return `₹${(v / 1e7).toFixed(2)}Cr`;
  if (Math.abs(v) >= 1e5) return `₹${(v / 1e5).toFixed(2)}L`;
  return "₹" + new Intl.NumberFormat("en-IN", { maximumFractionDigits: 0 }).format(Math.round(v));
};
const pad = (s, n) => String(s ?? "").padEnd(n).slice(0, n);
const rp = (s, n) => String(s ?? "").padStart(n);

token = (await call("POST", "/auth/login", { login: "owner@sivayaan", password: "Owner@123" })).accessToken;

console.log("\n=== 1. PURCHASE REGISTER vs STOCK VALUE ===\n");
const reg = await get("/reports/inventory/purchase-register");
console.log("  columns:", reg.columns.join(" | "));
const sums = {};
reg.columns.forEach((c, i) => {
  const nums = reg.rows.map((r) => r[i]).filter((v) => typeof v === "number");
  if (nums.length) sums[c] = nums.reduce((a, b) => a + b, 0);
});
for (const [c, v] of Object.entries(sums)) console.log("  " + pad(c, 22) + rp(L(v), 14));
console.log(`  rows: ${reg.rows.length}`);

console.log("\n=== 2. PURCHASES: what was actually billed ===\n");
const purchases = (await get("/purchases?pageSize=200")).items;
let gross = 0, paid = 0;
for (const p of purchases) { gross += p.totalAmount; paid += (p.paidAmount ?? 0); }
console.log(`  purchases            ${rp(purchases.length, 8)}`);
console.log(`  invoiced (landed)    ${rp(L(gross), 14)}`);
console.log(`  paid to suppliers    ${rp(L(paid), 14)}`);
console.log(`  SUPPLIER PAYABLE     ${rp(L(gross - paid), 14)}`);
console.log(`  statuses: ${[...new Set(purchases.map((p) => p.status))].join(", ")}`);

console.log("\n=== 3. CONTRACTOR COMMITMENT vs PAID ===\n");
const contracts = (await get("/contracts?pageSize=200")).items;
let contracted = 0, cPaid = 0;
for (const c of contracts) { contracted += c.contractAmount; cPaid += c.totalPaid; }
console.log(`  work orders          ${rp(contracts.length, 8)}`);
console.log(`  contracted           ${rp(L(contracted), 14)}`);
console.log(`  paid so far          ${rp(L(cPaid), 14)}`);
console.log(`  COMMITTED, UNPAID    ${rp(L(contracted - cPaid), 14)}   <- not in any villa's "total cost"`);

console.log("\n=== 4. EMPLOYEES: salary that reaches no villa ===\n");
const emps = (await get("/employees?pageSize=100")).items;
let monthly = 0;
for (const e of emps) {
  monthly += e.monthlySalary;
  console.log("  " + pad(e.name, 18) + pad(e.designation ?? "—", 18) + rp(L(e.monthlySalary), 12) +
    rp(`paid ${L(e.totalPaid)}`, 18));
}
console.log(`\n  monthly payroll      ${rp(L(monthly), 14)}`);
console.log(`  ~10 months of work   ${rp(L(monthly * 10), 14)}   <- appears in NO cost figure`);

console.log("\n=== 5. REVENUE RECOGNITION ===\n");
const projects = (await get("/projects?pageSize=100")).items;
console.log("  " + pad("Villa", 11) + rp("Done", 6) + rp("Sale", 11) + rp("Cost", 11) +
  rp("App says P/L", 14) + rp("Earned rev", 12) + rp("Honest P/L", 12));
let appPl = 0, honestPl = 0;
for (const p of projects) {
  const s = await get(`/projects/${p.id}/summary`);
  const sale = s.contractSaleValue ?? 0;
  if (!sale) continue;
  const earned = sale * (p.completionPercent / 100);
  const app = sale - s.totalCost;
  const honest = earned - s.totalCost;
  appPl += app; honestPl += honest;
  console.log("  " + pad(p.name, 11) + rp(`${p.completionPercent}%`, 6) + rp(L(sale), 11) +
    rp(L(s.totalCost), 11) + rp(L(app), 14) + rp(L(earned), 12) + rp(L(honest), 12));
}
console.log("\n  " + pad("TOTAL", 45) + rp(L(appPl), 14) + rp("", 12) + rp(L(honestPl), 12));
console.log(`\n  The app overstates profit by ${L(appPl - honestPl)} because it credits the full`);
console.log("  contracted sale value of a half-built villa against the cost incurred so far.");

console.log("\n=== 6. BUDGET vs PROGRESS ===\n");
console.log("  " + pad("Villa", 11) + rp("Done", 6) + rp("Estimate", 11) + rp("Spent", 11) +
  rp("Expected", 11) + rp("Burn rate", 12));
for (const p of projects) {
  const s = await get(`/projects/${p.id}/summary`);
  if (!p.completionPercent) continue;
  const expected = s.estimatedCost * (p.completionPercent / 100);
  const ratio = expected ? (s.totalCost / expected) : 0;
  console.log("  " + pad(p.name, 11) + rp(`${p.completionPercent}%`, 6) + rp(L(s.estimatedCost), 11) +
    rp(L(s.totalCost), 11) + rp(L(expected), 11) + rp(`${(ratio * 100).toFixed(0)}%`, 12));
}

console.log("\n=== 7. COMPANY SUMMARY (what the app reports) ===\n");
const co = await get("/reports/company/summary");
co.rows.forEach((r) => console.log("  " + pad(r[0], 34) + rp(typeof r[1] === "number" ? L(r[1]) : r[1], 16)));

console.log("\n=== 8. APPROVAL TRAIL ===\n");
const allApprovals = await get("/approvals?pendingOnly=false&pageSize=500");
const byType = {};
for (const a of allApprovals.items) byType[a.entityType] = (byType[a.entityType] ?? 0) + 1;
for (const [t, n] of Object.entries(byType)) console.log("  " + pad(t, 24) + rp(n, 6));
console.log("  " + pad("TOTAL", 24) + rp(allApprovals.total, 6));
const pending = await get("/approvals/count");
console.log("  " + pad("still pending", 24) + rp(pending.pending, 6));
console.log();
