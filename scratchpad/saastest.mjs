const B = "http://localhost:6051";
async function call(path, { method = "GET", body, token } = {}) {
  const res = await fetch(B + path, {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const json = await res.json().catch(() => ({}));
  return { status: res.status, ok: res.ok && json.success !== false, json };
}
const must = (c, msg) => { if (!c) { console.log("  ✗ FAIL: " + msg); process.exitCode = 1; } else console.log("  ✓ " + msg); };

console.log("=== 1. Founding tenant signs in ===");
const owner = await call("/api/auth/login", { method: "POST", body: { login: "owner@swarnakshi", password: "Owner@123" } });
must(owner.ok && owner.json.data.kind === "tenant", "owner@swarnakshi signs in as a tenant user");
const ownerTok = owner.json.data.accessToken;
console.log(`    company: ${owner.json.data.company.name} (${owner.json.data.company.code}), licence ${owner.json.data.company.licenseExpiresOn}`);
const sites = await call("/api/sites?pageSize=50", { token: ownerTok });
console.log(`    sees ${sites.json.data.items.length} site(s), ${(await call("/api/materials?pageSize=200", { token: ownerTok })).json.data.total} materials`);

console.log("\n=== 2. EnterpriseAdmin signs in (no '@') ===");
const ent = await call("/api/auth/login", { method: "POST", body: { login: "EnterpriseAdmin", password: "SivAyAAn@HMS" } });
must(ent.ok && ent.json.data.kind === "platform", "EnterpriseAdmin signs in as a platform operator");
const entTok = ent.json.data.accessToken;

console.log("\n=== 3. EnterpriseAdmin is locked OUT of company data ===");
for (const p of ["/api/sites", "/api/projects", "/api/dashboard", "/api/materials", "/api/users"]) {
  const r = await call(p, { token: entTok });
  must(r.status === 403, `${p} -> 403 for a platform token`);
}

console.log("\n=== 4. A second company registers ===");
const code = "acme" + Date.now().toString().slice(-5);
const reg = await call("/api/register", {
  method: "POST",
  body: { companyName: "Acme Builders", companyCode: code, username: "ravi", password: "Ravi@12345", confirmPassword: "Ravi@12345", contactEmail: "ravi@acme.example" },
});
must(reg.ok, `registered '${code}' -> login ${reg.json.data?.login}`);
const mismatch = await call("/api/register", { method: "POST", body: { companyName: "X", companyCode: code + "b", username: "a1b", password: "Password1", confirmPassword: "Password2" } });
must(mismatch.status === 400, "mismatched password confirmation rejected");
const dupe = await call("/api/register", { method: "POST", body: { companyName: "Other Name", companyCode: code, username: "someone", password: "Password1", confirmPassword: "Password1" } });
must(dupe.status === 409, "duplicate company CODE rejected");
const dupeName = await call("/api/register", { method: "POST", body: { companyName: "Acme Builders", companyCode: code + "x", username: "someone", password: "Password1", confirmPassword: "Password1" } });
must(dupeName.ok, "duplicate company NAME allowed");

console.log("\n=== 5. The new tenant is isolated and fully provisioned ===");
const acme = await call("/api/auth/login", { method: "POST", body: { login: `ravi@${code}`, password: "Ravi@12345" } });
must(acme.ok, `ravi@${code} signs in`);
const acmeTok = acme.json.data.accessToken;
const acmeSites = await call("/api/sites?pageSize=50", { token: acmeTok });
must(acmeSites.json.data.items.length === 0, "new company sees ZERO of Swarnakshi's sites");
const acmeMats = await call("/api/materials?pageSize=5", { token: acmeTok });
must(acmeMats.json.data.total >= 40, `new company got its own material catalogue (${acmeMats.json.data.total} materials)`);
const acmeUsers = await call("/api/users", { token: acmeTok });
must(acmeUsers.json.data.length === 1 && acmeUsers.json.data[0].login === `ravi@${code}`, "new company sees only its own user");

console.log("\n=== 6. Same username in two companies ===");
const dupUser = await call("/api/users", { method: "POST", token: acmeTok, body: { name: "Owner Two", username: "owner", password: "Owner@1234", role: 1 } });
must(dupUser.ok, "'owner' can exist in Acme too — usernames are per-company");

console.log("\n=== 7. EnterpriseAdmin manages licences ===");
const list = await call("/api/platform/companies", { token: entTok });
must(list.ok && list.json.data.length >= 2, `console lists ${list.json.data?.length} companies`);
const target = list.json.data.find(c => c.code === code);
console.log(`    ${target.name} (${target.code}): expires ${target.licenseExpiresOn}, ${target.daysToExpiry}d left, ${target.userCount} users`);
must(target.daysToExpiry <= 31 && target.daysToExpiry > 0, "self-registered company got a 30-day trial");

const expired = await call(`/api/platform/companies/${target.id}/license`, { method: "PUT", token: entTok, body: { expiresOn: "2020-01-01", notes: "expiry test" } });
must(expired.ok && expired.json.data.isExpired, "licence set into the past — company now marked expired");

console.log("\n=== 8. An expired licence locks the tenant out ===");
const blocked = await call("/api/sites", { token: acmeTok });
must(blocked.status === 402, `existing token refused mid-session -> 402 (${blocked.json.message?.slice(0, 60)}…)`);
const blockedLogin = await call("/api/auth/login", { method: "POST", body: { login: `ravi@${code}`, password: "Ravi@12345" } });
must(blockedLogin.status === 402, "and sign-in is refused at the door");

console.log("\n=== 9. EnterpriseAdmin renews it ===");
const renewed = await call(`/api/platform/companies/${target.id}/license/extend`, { method: "POST", token: entTok, body: { days: 365 } });
must(renewed.ok && !renewed.json.data.isExpired && renewed.json.data.daysToExpiry > 360, `renewed: ${renewed.json.data.daysToExpiry} days left (extended from today, not from the lapsed date)`);
const backIn = await call("/api/auth/login", { method: "POST", body: { login: `ravi@${code}`, password: "Ravi@12345" } });
must(backIn.ok, "the company can sign in again");

console.log("\n=== 10. EnterpriseAdmin resets a company admin's password ===");
const admin = (await call(`/api/platform/companies/${target.id}`, { token: entTok })).json.data.admins.find(a => a.username === "ravi");
const reset = await call(`/api/platform/companies/${target.id}/reset-password`, {
  method: "POST", token: entTok, body: { userId: admin.userId, newPassword: "Fresh@12345", confirmPassword: "Fresh@12345" },
});
must(reset.ok, `reset password for ${reset.json.data?.login}`);
must((await call("/api/auth/login", { method: "POST", body: { login: `ravi@${code}`, password: "Fresh@12345" } })).ok, "new password works");
must((await call("/api/auth/login", { method: "POST", body: { login: `ravi@${code}`, password: "Ravi@12345" } })).status === 401, "old password no longer works");
must((await call("/api/sites", { token: backIn.json.data.accessToken })).status === 401, "sessions from before the reset are revoked");

console.log("\n=== 11. A tenant token is locked OUT of the platform console ===");
for (const p of ["/api/platform/companies"]) {
  must((await call(p, { token: ownerTok })).status === 403, `${p} -> 403 for a tenant token`);
}
must((await call("/api/sites", { token: ownerTok })).ok, "meanwhile Swarnakshi still works normally");

console.log(process.exitCode ? "\nSOME CHECKS FAILED" : "\nALL SaaS CHECKS PASSED");
