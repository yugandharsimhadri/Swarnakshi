// Walks the six use cases exactly as docs/07-handover.md §6c tells a teammate to,
// against a FRESH database, and checks every number the doc promises.
const B = "http://localhost:6051";
let T = "";
const H = () => ({ "Content-Type": "application/json", ...(T ? { Authorization: `Bearer ${T}` } : {}) });
async function call(path, { method = "GET", body } = {}) {
  const r = await fetch(B + path, { method, headers: H(), body: body ? JSON.stringify(body) : undefined });
  const j = await r.json().catch(() => ({}));
  return { status: r.status, ok: r.ok && j.success !== false, data: j.data, message: j.message };
}
let bad = 0;
const check = (cond, label, got) => {
  if (cond) console.log(`  ✓ ${label}`);
  else { console.log(`  ✗ ${label}  — got ${got}`); bad++; }
};

T = (await call("/api/auth/login", { method: "POST", body: { login: "owner@swarnakshi", password: "Owner@123" } })).data.accessToken;

const site = (await call("/api/sites?pageSize=50")).data.items.find((s) => s.name === "Green Valley");
const villa101 = (await call("/api/projects?pageSize=50")).data.items.find((p) => p.name === "Villa 101");
const villa103 = (await call("/api/projects?pageSize=50")).data.items.find((p) => p.name === "Villa 103");
const cement = (await call("/api/materials?q=OPC&pageSize=5")).data.items[0];
const supplier = (await call("/api/suppliers?pageSize=5")).data.items[0];
const method = (await call("/api/payment-methods")).data.find((m) => m.name === "Bank Transfer");

const buy = async (qty, rate, deliverTo = null, remarks = null) => {
  const c = await call("/api/purchases", { method: "POST", body: {
    supplierId: supplier.id, siteId: site.id, date: "2026-09-01", otherCharges: 0, remarks,
    items: [{ materialId: cement.id, unitId: cement.unitId, quantity: qty, rate, discount: 0, taxAmount: 0, deliverToProjectId: deliverTo }] } });
  if (!c.ok) return c;
  return call(`/api/purchases/${c.data.id}/submit`, { method: "POST" });
};
const stock = async () => (await call(`/api/inventory?siteId=${site.id}`)).data[0];
const cost = async (pid) => (await call(`/api/projects/${pid}/summary`)).data;

console.log("\n── Use case 3: add cement to the store ──");
await buy(100, 400, null, "Lorry AP09 XX 1234");
let s = await stock();
check(s.quantity === 100 && s.averageRate === 400 && s.value === 40000, "100 BAG @ ₹400 = ₹40,000", `${s.quantity}@${s.averageRate}=${s.value}`);
check((await cost(villa101.id)).materialCost === 0, "Villa 101 material cost ₹0 — buying is not spending", (await cost(villa101.id)).materialCost);
await buy(100, 450);
s = await stock();
check(s.quantity === 200 && s.averageRate === 425, "second delivery blends to 200 @ ₹425", `${s.quantity}@${s.averageRate}`);

console.log("\n── Use case 1: store → villa ──");
const req = await call("/api/material-requests", { method: "POST", body: {
  projectId: villa101.id, requestType: 1, date: "2026-09-01", notes: "First-floor slab",
  items: [{ materialId: cement.id, unitId: cement.unitId, requestedQty: 50 }] } });
check(req.data.notes === "First-floor slab", "remark travels with the request", req.data.notes);
const preSubmit = await call(`/api/material-requests/${req.data.id}/issue`, { method: "POST", body: { items: null } });
check(preSubmit.status === 409, "issue refused before submit (use case 4)", preSubmit.status);
await call(`/api/material-requests/${req.data.id}/submit`, { method: "POST" });
const prePending = await call(`/api/material-requests/${req.data.id}/issue`, { method: "POST", body: { items: null } });
check(prePending.status === 409, "issue refused while pending approval (use case 4)", prePending.status);
check((await stock()).quantity === 200, "store untouched while pending", (await stock()).quantity);
const pend = (await call("/api/approvals?pendingOnly=true&pageSize=50")).data.items.find((a) => a.entityRef === req.data.txnNumber);
await call(`/api/approvals/${pend.id}/approve`, { method: "POST", body: { remarks: "ok", allowOverride: false } });
await call(`/api/material-requests/${req.data.id}/issue`, { method: "POST", body: { items: null } });
s = await stock();
const c1 = await cost(villa101.id);
check(s.quantity === 150 && s.value === 63750, "store 150 BAG, ₹63,750", `${s.quantity}, ${s.value}`);
check(c1.materialCost === 21250, "Villa 101 charged ₹21,250 (50 × ₹425)", c1.materialCost);
check(c1.materialCost + s.value === 85000, "₹21,250 + ₹63,750 = ₹85,000 purchased", c1.materialCost + s.value);

console.log("\n── Use case 2: purchase direct to villa ──");
const before = await stock();
await buy(100, 450, villa101.id, "Unloaded at Villa 101 direct");
const after = await stock();
check(after.quantity === before.quantity && after.averageRate === before.averageRate && after.value === before.value,
  `store unchanged: ${before.quantity}@${before.averageRate}=${before.value}`, `${after.quantity}@${after.averageRate}=${after.value}`);
check((await cost(villa101.id)).materialCost === 21250 + 45000, "Villa 101 gains exactly ₹45,000", (await cost(villa101.id)).materialCost);
const led = (await call(`/api/inventory/transactions?siteId=${site.id}&materialId=${cement.id}&pageSize=50`)).data.items;
check(led.some((t) => t.type === 2 && t.quantity === 100 && t.rate === 450), "ledger shows Purchase +100 @ ₹450");
check(led.some((t) => t.type === 4 && t.quantity === -100 && t.rate === 450 && t.projectName === "Villa 101"), "ledger shows Consumption −100 @ ₹450 → Villa 101");
const wrongSite = await buy(10, 400, villa103.id);
check(wrongSite.status === 409, "a project on another site is refused", wrongSite.status);

console.log("\n── Use case 5: customer payments ──");
await call("/api/customer-payments", { method: "POST", body: { projectId: villa101.id, date: "2026-09-01", amount: 1000000, paymentMethodId: method.id, reference: "NEFT-8891", description: "First instalment" } });
await call("/api/customer-payments", { method: "POST", body: { projectId: villa101.id, date: "2026-09-01", amount: 1500000, paymentMethodId: method.id, reference: "NEFT-9021", description: "Second instalment" } });
const cust = await cost(villa101.id);
check(cust.customerReceived === 2500000, "received ₹25,00,000", cust.customerReceived);
check(cust.customerOutstanding === 5500000, "outstanding ₹55,00,000 of ₹80,00,000", cust.customerOutstanding);
const noCustomer = await call("/api/customer-payments", { method: "POST", body: { projectId: villa103.id, date: "2026-09-01", amount: 1000, paymentMethodId: method.id } });
check(noCustomer.status === 409, "receipt on a project with no customer refused", noCustomer.status);

console.log("\n── Use case 6: simple entry, with remarks ──");
const minimal = await call("/api/purchases", { method: "POST", body: {
  supplierId: supplier.id, siteId: site.id, date: "2026-09-01", otherCharges: 0,
  items: [{ materialId: cement.id, unitId: cement.unitId, quantity: 10, rate: 400, discount: 0, taxAmount: 0 }] } });
check(minimal.ok && minimal.data.totalAmount === 4000, "a purchase needs only supplier, site, material, qty, rate", minimal.message);
const open = await call("/api/inventory/opening-stock", { method: "POST", body: { siteId: site.id, materialId: cement.id, quantity: 5, rate: 400, date: "2026-09-01", remarks: "Counted at handover" } });
check(open.data?.remarks === "Counted at handover", "opening stock keeps its remark", open.data?.remarks);

console.log(bad === 0 ? "\nEVERY NUMBER IN THE HANDOVER WALKTHROUGH IS CORRECT" : `\n${bad} CLAIM(S) IN THE DOC ARE WRONG`);
process.exitCode = bad ? 1 : 0;
