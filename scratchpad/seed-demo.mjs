/**
 * Builds a realistic book of work through the public API — no direct database writes — so every
 * number it produces has passed the same validation, approval and posting code a real user would.
 *
 * It registers its own company rather than adding to the dev tenant, so the totals it prints are
 * attributable to exactly what this script created and nothing else.
 *
 *   node scratchpad/seed-demo.mjs            # build, then report
 *   node scratchpad/seed-demo.mjs --report   # report only, against an existing tenant
 */

const API = process.env.SWK_API ?? "http://localhost:6051/api";
const COMPANY_CODE = process.env.SWK_CODE ?? "sivayaan";
const COMPANY_NAME = "Sivayaan Constructions";
const USERNAME = "owner";
const PASSWORD = "Owner@123";

let token = null;

// ---------------------------------------------------------------------------
// transport
// ---------------------------------------------------------------------------

async function call(method, path, body) {
  const res = await fetch(`${API}${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json;
  try { json = text ? JSON.parse(text) : null; } catch { json = { raw: text }; }
  if (!res.ok) {
    const detail = json?.message ?? json?.title ?? text.slice(0, 300);
    throw new Error(`${method} ${path} → ${res.status}: ${detail}`);
  }
  return json?.data ?? json;
}

const get = (p) => call("GET", p);
const post = (p, b) => call("POST", p, b);
const put = (p, b) => call("PUT", p, b);

// ---------------------------------------------------------------------------
// helpers
// ---------------------------------------------------------------------------

const money = (n) =>
  "₹" + new Intl.NumberFormat("en-IN", { maximumFractionDigits: 0 }).format(Math.round(n ?? 0));

const lakh = (n) => {
  const v = Number(n ?? 0);
  if (Math.abs(v) >= 1e7) return `₹${(v / 1e7).toFixed(2)}Cr`;
  if (Math.abs(v) >= 1e5) return `₹${(v / 1e5).toFixed(2)}L`;
  return money(v);
};

const pad = (s, n) => String(s ?? "").padEnd(n).slice(0, n);
const rpad = (s, n) => String(s ?? "").padStart(n);
const rule = (n = 96) => console.log("─".repeat(n));

/** Dates spread across the last 10 months so month-scoped dashboard tiles have something in them. */
const DAY = 86_400_000;
const today = new Date();
const daysAgo = (d) => new Date(today.getTime() - d * DAY).toISOString().slice(0, 10);

let approvalsApproved = 0;

/** Approve whatever is sitting in the queue for one entity, so posting actually happens. */
async function approveAll() {
  const queue = await get("/approvals?pendingOnly=true&pageSize=200");
  for (const a of queue.items) {
    await post(`/approvals/${a.id}/approve`, { remarks: "Checked on site.", allowOverride: false });
    approvalsApproved++;
  }
}

// ---------------------------------------------------------------------------
// sign in
// ---------------------------------------------------------------------------

async function signIn() {
  const login = `${USERNAME}@${COMPANY_CODE}`;
  try {
    const auth = await post("/auth/login", { login, password: PASSWORD });
    token = auth.accessToken;
    return { fresh: false, company: auth.company };
  } catch {
    const reg = await post("/register", {
      companyName: COMPANY_NAME,
      companyCode: COMPANY_CODE,
      username: USERNAME,
      password: PASSWORD,
      confirmPassword: PASSWORD,
      contactEmail: "owner@sivayaan.example",
      contactMobile: "9876500000",
    });
    const auth = await post("/auth/login", { login, password: PASSWORD });
    token = auth.accessToken;
    return { fresh: true, company: reg };
  }
}

// ---------------------------------------------------------------------------
// build
// ---------------------------------------------------------------------------

async function build() {
  // ---- reference data the tenant was provisioned with ----------------------
  const materials = (await get("/materials?pageSize=300&active=true")).items;
  const heads = await get("/expense-heads");
  const labourCats = await get("/labour-categories");
  const methods = await get("/payment-methods");

  const mat = (needle) => {
    const m = materials.find((x) => x.name.toLowerCase().includes(needle.toLowerCase()));
    if (!m) throw new Error(`No seeded material matching "${needle}"`);
    return m;
  };
  const head = (needle) =>
    heads.find((h) => h.name.toLowerCase().includes(needle.toLowerCase())) ?? heads[0];

  const cement = mat("OPC 53");
  const steel = mat("TMT Steel");
  const sand = mat("M-Sand");
  const aggregate = mat("20mm Aggregate");
  const brick = mat("Red Brick");
  const tiles = mat("Vitrified");
  const paint = mat("Interior Emulsion");
  const wire = mat("Electrical Wire");
  const pipe = mat("CPVC Pipe");

  const cash = methods.find((m) => /cash/i.test(m.name)) ?? methods[0];
  const bank = methods.find((m) => /bank|neft|transfer/i.test(m.name)) ?? methods[0];

  const hMaterial = head("material");
  const hLabour = head("labour");
  const hTransport = head("transport");
  const hMisc = head("misc") ?? head("other");

  // ---- sites --------------------------------------------------------------
  console.log("Creating sites…");
  const greenMeadows = await post("/sites", {
    name: "Green Meadows", city: "Hyderabad", state: "Telangana", pin: "500084",
    startDate: daysAgo(300), status: 1, notes: "Phase 1 — 6 villas on 4 acres.",
  });
  const palmGrove = await post("/sites", {
    name: "Palm Grove", city: "Vijayawada", state: "Andhra Pradesh", pin: "520010",
    startDate: daysAgo(210), status: 1, notes: "Phase 1 — 4 villas on the canal road.",
  });

  // ---- parties ------------------------------------------------------------
  console.log("Creating suppliers, contractors, customers, employees…");
  const supplier = async (name, type) =>
    post("/suppliers", { name, companyName: name, mobile: "98765" + Math.floor(10000 + Math.random() * 89999), type });

  const sriBalaji = await supplier("Sri Balaji Traders");       // cement, steel
  const lakshmi = await supplier("Lakshmi Building Material");   // sand, aggregate, brick
  const anjaneya = await supplier("Anjaneya Hardware");          // finishing

  const contractor = async (name, type) =>
    post("/contractors", { name, companyName: name, mobile: "98650" + Math.floor(10000 + Math.random() * 89999), type });

  const ramesh = await contractor("Ramesh Plumbing Works", "Plumbing");
  const srinivas = await contractor("Srinivas Electricals", "Electrical");
  const kumar = await contractor("Kumar Masonry", "Civil");
  const venkat = await contractor("Venkat Painters", "Painting");

  const customer = async (name, mobile) => post("/customers", { name, mobile });
  const customers = {
    rao: await customer("Prasad Rao", "9848011111"),
    reddy: await customer("Sunitha Reddy", "9848022222"),
    khan: await customer("Imran Khan", "9848033333"),
    naidu: await customer("Lakshmi Naidu", "9848044444"),
    sharma: await customer("Anil Sharma", "9848055555"),
    iyer: await customer("Meera Iyer", "9848066666"),
  };

  const employee = async (name, phone, salary, siteId, designation) =>
    post("/employees", {
      name, phone, monthlySalary: salary, joinDate: daysAgo(280),
      designation, siteId, isActive: true,
    });

  await employee("Suresh Kumar", "9700011111", 32000, greenMeadows.id, "Site Supervisor");
  await employee("Ravi Teja", "9700022222", 28000, palmGrove.id, "Site Supervisor");
  await employee("Mahesh Babu", "9700033333", 24000, greenMeadows.id, "Storekeeper");
  await employee("Anitha Rani", "9700044444", 35000, null, "Accounts");

  // ---- villas -------------------------------------------------------------
  // Three finished, three half-built, three barely started, one still on paper.
  console.log("Creating villas…");
  const villaSpec = [
    // site, name, villa, customer, estimate, sale, status, %, startedDaysAgo
    [greenMeadows, "Villa 101", "101", customers.rao, 4_200_000, 5_600_000, 3, 100, 290],
    [greenMeadows, "Villa 102", "102", customers.reddy, 4_200_000, 5_500_000, 3, 100, 285],
    [greenMeadows, "Villa 103", "103", customers.khan, 4_500_000, 5_900_000, 3, 100, 280],
    [greenMeadows, "Villa 104", "104", customers.naidu, 4_400_000, 5_800_000, 1, 50, 190],
    [greenMeadows, "Villa 105", "105", customers.sharma, 4_400_000, 5_800_000, 1, 50, 185],
    [greenMeadows, "Villa 106", "106", null, 4_600_000, null, 1, 50, 180],
    [palmGrove, "Villa 201", "201", customers.iyer, 3_900_000, 5_200_000, 1, 10, 95],
    [palmGrove, "Villa 202", "202", null, 3_900_000, null, 1, 10, 90],
    [palmGrove, "Villa 203", "203", null, 4_000_000, null, 1, 10, 85],
    [palmGrove, "Villa 204", "204", null, 4_000_000, null, 0, 0, null],
  ];

  const villas = [];
  for (const [site, name, villaNo, cust, est, sale, status, pct, started] of villaSpec) {
    const p = await post("/projects", {
      name, villaNumber: villaNo, siteId: site.id,
      customerId: cust?.id ?? null,
      estimatedCost: est, contractSaleValue: sale,
      status, completionPercent: pct,
      startDate: started ? daysAgo(started) : null,
    });
    villas.push({ ...p, site, customer: cust, pct });
  }

  const at = (pct) => villas.filter((v) => v.pct === pct);
  const done = at(100), half = at(50), early = at(10);

  // ---- stock into the two stores -----------------------------------------
  // Bought as real invoices so the purchase register and the stock ledger agree.
  console.log("Buying stock into site stores…");
  const buy = async (site, sup, day, lines, invoice, remarks) => {
    const created = await post("/purchases", {
      supplierId: sup.id, siteId: site.id, projectId: null,
      invoiceNumber: invoice, invoiceDate: daysAgo(day), date: daysAgo(day),
      otherCharges: 0, remarks,
      items: lines.map(([m, qty, rate, deliverTo]) => ({
        materialId: m.id, unitId: m.unitId, quantity: qty, rate,
        discount: 0, taxAmount: Math.round(qty * rate * 0.18),
        deliverToProjectId: deliverTo ?? null,
      })),
    });
    const submitted = await post(`/purchases/${created.id}/submit`);
    // Purchases now wait for the owner, so nothing reaches stock until this runs.
    await approveAll();
    return submitted;
  };

  await buy(greenMeadows, sriBalaji, 270, [[cement, 1200, 420], [steel, 9000, 62]], "SBT/24-25/118", "Two lorries, received by Mahesh.");
  await buy(greenMeadows, lakshmi, 265, [[sand, 900, 48], [aggregate, 1100, 41], [brick, 34000, 9]], "LBM/1204", "Brick count verified at gate.");
  await buy(greenMeadows, sriBalaji, 190, [[cement, 900, 445], [steel, 6500, 66]], "SBT/24-25/402", "Rate revised from 420.");
  // A second lot at a different rate, so the weighted average is actually doing something.
  await buy(greenMeadows, lakshmi, 150, [[sand, 650, 53], [aggregate, 800, 45], [brick, 30000, 9.5]], "LBM/1339", "Monsoon rates.");
  await buy(greenMeadows, anjaneya, 120, [[tiles, 2400, 78], [paint, 320, 285], [wire, 3200, 34], [pipe, 900, 96]], "AH/2211", "Finishing lot for 101-103.");
  await buy(palmGrove, lakshmi, 200, [[sand, 600, 52], [aggregate, 700, 44], [brick, 26000, 10]], "LBM/1387", null);
  await buy(palmGrove, sriBalaji, 180, [[cement, 700, 440], [steel, 4800, 65]], "SBT/24-25/455", "Delivered to Palm Grove store.");
  await buy(palmGrove, anjaneya, 60, [[pipe, 400, 98], [wire, 1400, 35]], "AH/2402", null);

  // ---- material bought for one villa and taken straight there --------------
  // Passes through the store — received then issued in the same post — so the store's
  // quantity and average rate are left exactly as they were.
  console.log("Recording direct-to-villa purchases…");
  await buy(greenMeadows, anjaneya, 100, [[tiles, 800, 82, done[0].id]], "AH/2260", "Owner picked the tile himself for 101.");
  await buy(greenMeadows, anjaneya, 95, [[paint, 140, 292, done[2].id]], "AH/2271", "Shade change requested for 103.");
  await buy(palmGrove, sriBalaji, 70, [[cement, 250, 448, early[0].id]], "SBT/24-25/511", "Straight to 201 for the raft.");

  // ---- issuing stock from the store to the villas -------------------------
  console.log("Issuing material from store to villas…");
  const issue = async (villa, day, lines, notes) => {
    const created = await post("/material-requests", {
      projectId: villa.id, requestType: 1, date: daysAgo(day), notes,
      items: lines.map(([m, qty]) => ({
        materialId: m.id, unitId: m.unitId, requestedQty: qty,
        expenseHeadId: hMaterial.id, expenseSubheadId: null,
      })),
    });
    await post(`/material-requests/${created.id}/submit`);
    await approveAll();
    return post(`/material-requests/${created.id}/issue`, { items: null });
  };

  // Finished villas — a full build's worth of material.
  for (const [i, v] of done.entries()) {
    await issue(v, 240 - i * 5, [[cement, 320], [steel, 2400], [sand, 240], [aggregate, 290], [brick, 11000]], "Structure — footing to slab.");
    await issue(v, 150 - i * 5, [[tiles, 520], [paint, 70], [wire, 780], [pipe, 210]], "Finishing.");
  }
  // Half-built — structure done, no finishing yet.
  for (const [i, v] of half.entries()) {
    await issue(v, 140 - i * 5, [[cement, 260], [steel, 1900], [sand, 180], [aggregate, 220], [brick, 8000]], "Structure up to first slab.");
  }
  // Barely started — foundation only.
  for (const [i, v] of early.entries()) {
    await issue(v, 70 - i * 5, [[cement, 80], [steel, 520], [sand, 70], [aggregate, 90]], "Excavation and raft.");
  }

  // ---- work orders and contractor payments --------------------------------
  console.log("Raising work orders and paying contractors…");
  const workOrder = async (villa, con, category, amount, estimate, status, day) =>
    post("/contracts", {
      projectId: villa.id, contractorId: con.id, workCategory: category,
      description: `${category} for ${villa.name}`,
      estimatedCost: estimate, contractAmount: amount,
      startDate: daysAgo(day), expectedCompletion: daysAgo(day - 60),
      paymentTerms: "30% advance, balance on completion", workStatus: status,
    });

  const payContractor = async (villa, con, wo, amount, kind, day, ref) => {
    const created = await post("/contractor-payments", {
      contractorId: con.id, projectId: villa.id, contractWorkId: wo?.id ?? null,
      date: daysAgo(day), amount, paymentMethodId: bank.id,
      referenceNumber: ref, description: null, paymentKind: kind,
    });
    await post(`/contractor-payments/${created.id}/submit`);
    await approveAll();
  };

  for (const [i, v] of done.entries()) {
    const masonry = await workOrder(v, kumar, "Masonry & plastering", 620_000, 600_000, 2, 250 - i * 5);
    const plumb = await workOrder(v, ramesh, "Plumbing", 185_000, 180_000, 2, 190 - i * 5);
    const elec = await workOrder(v, srinivas, "Electrical", 210_000, 200_000, 2, 185 - i * 5);
    const paintWo = await workOrder(v, venkat, "Painting", 165_000, 160_000, 2, 130 - i * 5);
    await payContractor(v, kumar, masonry, 186_000, 1, 245 - i * 5, "NEFT/8801");
    await payContractor(v, kumar, masonry, 434_000, 3, 160 - i * 5, "NEFT/9142");
    await payContractor(v, ramesh, plumb, 185_000, 3, 150 - i * 5, "NEFT/9210");
    await payContractor(v, srinivas, elec, 210_000, 3, 145 - i * 5, "NEFT/9288");
    await payContractor(v, venkat, paintWo, 165_000, 3, 110 - i * 5, "NEFT/9410");
  }
  for (const [i, v] of half.entries()) {
    const masonry = await workOrder(v, kumar, "Masonry & plastering", 640_000, 620_000, 1, 175 - i * 5);
    const plumb = await workOrder(v, ramesh, "Plumbing", 190_000, 185_000, 1, 120 - i * 5);
    await payContractor(v, kumar, masonry, 192_000, 1, 170 - i * 5, "NEFT/9502");
    await payContractor(v, kumar, masonry, 160_000, 2, 90 - i * 5, "NEFT/9744");
    await payContractor(v, ramesh, plumb, 57_000, 1, 85 - i * 5, "NEFT/9801");
  }
  for (const [i, v] of early.entries()) {
    const masonry = await workOrder(v, kumar, "Foundation & masonry", 580_000, 560_000, 1, 80 - i * 5);
    await payContractor(v, kumar, masonry, 174_000, 1, 75 - i * 5, "NEFT/9905");
  }

  // ---- day labour ---------------------------------------------------------
  console.log("Recording day labour…");
  const labour = async (villa, cat, amount, day, remarks) => {
    const created = await post("/labour", {
      projectId: villa.id, labourCategoryId: cat.id, periodType: 1,
      periodStart: daysAgo(day), periodEnd: daysAgo(day), amount,
      paymentMethodId: cash.id, paymentType: "Daily", remarks,
    });
    await post(`/labour/${created.id}/submit`);
    await approveAll();
  };
  for (const [i, v] of done.entries()) {
    await labour(v, labourCats[0], 46_000, 235 - i * 5, "Centring crew, 8 days.");
    await labour(v, labourCats[Math.min(1, labourCats.length - 1)], 38_000, 165 - i * 5, "Helpers during finishing.");
  }
  for (const [i, v] of half.entries()) {
    await labour(v, labourCats[0], 41_000, 135 - i * 5, "Centring crew.");
  }
  for (const [i, v] of early.entries()) {
    await labour(v, labourCats[0], 18_000, 65 - i * 5, "Excavation help.");
  }

  // ---- other site spend ---------------------------------------------------
  console.log("Recording expenses…");
  const expense = async (villa, h, amount, type, day, note) =>
    post("/expenses", {
      projectId: villa.id, date: daysAgo(day), expenseHeadId: h.id, expenseSubheadId: null,
      description: note, amount, expenseType: type, paymentStatus: 2, paymentMethodId: cash.id,
    });

  for (const [i, v] of done.entries()) {
    await expense(v, hTransport, 24_000, 5, 230 - i * 5, "Lorry hire, sand and jelly.");
    await expense(v, hMisc, 9_500, 7, 175 - i * 5, "Water tanker during curing.");
    await expense(v, hMisc, 6_200, 7, 120 - i * 5, "Tea and snacks for crew.");
  }
  for (const [i, v] of half.entries()) {
    await expense(v, hTransport, 21_000, 5, 130 - i * 5, "Lorry hire.");
    await expense(v, hMisc, 7_400, 7, 95 - i * 5, "Water tanker.");
  }
  for (const [i, v] of early.entries()) {
    await expense(v, hTransport, 12_500, 5, 60 - i * 5, "JCB hire for excavation.");
  }

  // ---- customer receipts --------------------------------------------------
  console.log("Recording customer receipts…");
  const receipt = async (villa, amount, day, ref, note) =>
    post("/customer-payments", {
      projectId: villa.id, date: daysAgo(day), amount,
      paymentMethodId: bank.id, reference: ref, description: note,
    });

  // Finished and handed over — paid in full bar a small retention on one.
  await receipt(done[0], 1_400_000, 280, "CHQ/100234", "Booking advance.");
  await receipt(done[0], 2_800_000, 180, "NEFT/44021", "On slab and brickwork.");
  await receipt(done[0], 1_400_000, 60, "NEFT/48800", "On handover.");
  await receipt(done[1], 1_375_000, 275, "CHQ/100241", "Booking advance.");
  await receipt(done[1], 2_750_000, 175, "NEFT/44190", "Stage 2.");
  await receipt(done[1], 1_375_000, 55, "NEFT/48930", "Final.");
  await receipt(done[2], 1_475_000, 270, "CHQ/100255", "Booking advance.");
  await receipt(done[2], 2_950_000, 170, "NEFT/44255", "Stage 2.");
  // 103 has ₹14.75L still outstanding — deliberately, to give the report something real to show.

  // Half-built — booking plus one stage.
  await receipt(half[0], 1_450_000, 185, "CHQ/100302", "Booking advance.");
  await receipt(half[0], 1_450_000, 80, "NEFT/47110", "On plinth.");
  await receipt(half[1], 1_450_000, 180, "CHQ/100310", "Booking advance.");
  // 106 has no customer yet — unsold inventory.

  // Barely started — booking only.
  await receipt(early[0], 1_040_000, 90, "CHQ/100401", "Booking advance.");

  await approveAll();
  console.log(`\nDone. ${approvalsApproved} items went through the approval queue.\n`);
}

// ---------------------------------------------------------------------------
// report
// ---------------------------------------------------------------------------

async function report() {
  const sites = (await get("/sites?pageSize=100")).items;
  const projects = (await get("/projects?pageSize=100")).items;

  const summaries = new Map();
  for (const p of projects) summaries.set(p.id, await get(`/projects/${p.id}/summary`));

  const stockBySite = new Map();
  for (const s of sites) stockBySite.set(s.id, await get(`/inventory?siteId=${s.id}`));

  const siteOf = new Map(sites.map((s) => [s.id, s]));

  // ---- per villa ----------------------------------------------------------
  rule();
  console.log("PROFIT / LOSS PER VILLA");
  rule();
  console.log(
    pad("Villa", 12) + pad("Site", 15) + rpad("Done", 6) + rpad("Material", 12) +
    rpad("Labour", 10) + rpad("Contract", 11) + rpad("Other", 9) +
    rpad("Total cost", 12) + rpad("Sale", 11) + rpad("P / L", 11),
  );
  rule();

  let gCost = 0, gSale = 0, gRecd = 0, gMat = 0, gLab = 0, gCon = 0, gOth = 0;
  const perSite = new Map(sites.map((s) => [s.id, { cost: 0, sale: 0, recd: 0, villas: 0 }]));

  for (const p of projects) {
    const s = summaries.get(p.id);
    const site = siteOf.get(p.siteId);
    const sale = s.contractSaleValue ?? 0;
    const pl = sale ? sale - s.totalCost : null;

    console.log(
      pad(p.name, 12) + pad(site?.name ?? "—", 15) + rpad(`${p.completionPercent}%`, 6) +
      rpad(lakh(s.materialCost), 12) + rpad(lakh(s.labourCost), 10) +
      rpad(lakh(s.contractorCost), 11) + rpad(lakh(s.otherCost), 9) +
      rpad(lakh(s.totalCost), 12) + rpad(sale ? lakh(sale) : "unsold", 11) +
      rpad(pl === null ? "—" : lakh(pl), 11),
    );

    gCost += s.totalCost; gSale += sale; gRecd += s.customerReceived;
    gMat += s.materialCost; gLab += s.labourCost; gCon += s.contractorCost; gOth += s.otherCost;
    const agg = perSite.get(p.siteId);
    agg.cost += s.totalCost; agg.sale += sale; agg.recd += s.customerReceived; agg.villas++;
  }

  // ---- per site -----------------------------------------------------------
  rule();
  console.log("PER SITE");
  rule();
  console.log(
    pad("Site", 18) + rpad("Villas", 8) + rpad("Build cost", 13) +
    rpad("Stock value", 13) + rpad("Sale value", 13) + rpad("Received", 13) + rpad("P / L", 13),
  );
  rule();

  let gStock = 0;
  for (const s of sites) {
    const a = perSite.get(s.id);
    const stock = (stockBySite.get(s.id) ?? []).reduce((t, r) => t + r.value, 0);
    gStock += stock;
    console.log(
      pad(s.name, 18) + rpad(a.villas, 8) + rpad(lakh(a.cost), 13) +
      rpad(lakh(stock), 13) + rpad(lakh(a.sale), 13) + rpad(lakh(a.recd), 13) +
      rpad(a.sale ? lakh(a.sale - a.cost) : "—", 13),
    );
  }

  // ---- per customer -------------------------------------------------------
  const custRows = new Map();
  for (const p of projects) {
    if (!p.customerId) continue;
    const s = summaries.get(p.id);
    const r = custRows.get(p.customerId) ?? { name: p.customerName, villas: [], sale: 0, recd: 0, out: 0, cost: 0 };
    r.villas.push(p.name);
    r.sale += s.contractSaleValue ?? 0;
    r.recd += s.customerReceived;
    r.out += s.customerOutstanding;
    r.cost += s.totalCost;
    custRows.set(p.customerId, r);
  }

  rule();
  console.log("PER CUSTOMER");
  rule();
  console.log(pad("Customer", 18) + pad("Villa", 12) + rpad("Sale", 13) + rpad("Received", 13) + rpad("Outstanding", 14) + rpad("Margin", 12));
  rule();
  for (const r of custRows.values()) {
    console.log(
      pad(r.name, 18) + pad(r.villas.join(", "), 12) + rpad(lakh(r.sale), 13) +
      rpad(lakh(r.recd), 13) + rpad(lakh(r.out), 14) + rpad(lakh(r.sale - r.cost), 12),
    );
  }

  // ---- inventory ----------------------------------------------------------
  rule();
  console.log("INVENTORY ON HAND");
  rule();
  for (const s of sites) {
    const rows = stockBySite.get(s.id) ?? [];
    const total = rows.reduce((t, r) => t + r.value, 0);
    console.log(`\n  ${s.name} — ${rows.length} materials, ${lakh(total)}`);
    for (const r of rows.filter((x) => x.quantity > 0).sort((a, b) => b.value - a.value).slice(0, 8)) {
      console.log(
        "    " + pad(r.materialName, 26) + rpad(new Intl.NumberFormat("en-IN").format(r.quantity), 10) +
        " " + pad(r.unitCode, 5) + rpad(money(r.averageRate), 10) + rpad(lakh(r.value), 12),
      );
    }
  }

  // ---- global -------------------------------------------------------------
  const purchased = gMat + gStock;
  rule();
  console.log("COMPANY");
  rule();
  const line = (k, v) => console.log("  " + pad(k, 34) + rpad(v, 16));
  line("Villas", `${projects.length}`);
  line("Material consumed into villas", lakh(gMat));
  line("Labour", lakh(gLab));
  line("Contractors", lakh(gCon));
  line("Other site spend", lakh(gOth));
  line("Total build cost", lakh(gCost));
  console.log();
  line("Stock still on hand", lakh(gStock));
  line("Total material purchased", lakh(purchased));
  console.log();
  line("Contracted sale value", lakh(gSale));
  line("Received from customers", lakh(gRecd));
  line("Outstanding from customers", lakh(gSale - gRecd));
  line("Gross profit (sold villas)", lakh(gSale - gCost));
  console.log();

  // ---- the identity that must hold ---------------------------------------
  const register = await get("/reports/inventory/purchase-register");
  const registerTotal = register.rows.reduce((t, r) => {
    const i = register.columns.findIndex((c) => /total|value|amount/i.test(c));
    return t + (typeof r[i] === "number" ? r[i] : 0);
  }, 0);

  rule();
  console.log("RECONCILIATION");
  rule();
  console.log(`  consumed into villas   ${rpad(lakh(gMat), 14)}`);
  console.log(`  + still in the stores  ${rpad(lakh(gStock), 14)}`);
  console.log(`  = accounted for        ${rpad(lakh(purchased), 14)}`);
  console.log(`  purchase register      ${rpad(lakh(registerTotal), 14)}   (includes tax & direct-to-villa)`);
  console.log();
}

// ---------------------------------------------------------------------------

const session = await signIn();
console.log(`\n${session.fresh ? "Registered" : "Signed in to"} ${COMPANY_NAME} (${COMPANY_CODE})\n`);
if (!process.argv.includes("--report")) await build();
await report();
