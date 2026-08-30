/** Indian-format currency + numbers. */
const inr = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 0 });
const inr2 = new Intl.NumberFormat("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function money(n: number | null | undefined, decimals = false): string {
  if (n === null || n === undefined) return "—";
  return `₹${(decimals ? inr2 : inr).format(n)}`;
}

/** Compact form for cards: ₹1.2L, ₹3.4Cr */
export function moneyShort(n: number | null | undefined): string {
  if (n === null || n === undefined) return "—";
  const abs = Math.abs(n);
  if (abs >= 1e7) return `₹${(n / 1e7).toFixed(2)}Cr`;
  if (abs >= 1e5) return `₹${(n / 1e5).toFixed(2)}L`;
  if (abs >= 1e3) return `₹${(n / 1e3).toFixed(1)}K`;
  return `₹${n.toFixed(0)}`;
}

export function num(n: number | null | undefined): string {
  return n === null || n === undefined ? "—" : inr.format(n);
}

export function dateStr(s: string | null | undefined): string {
  if (!s) return "—";
  return new Date(s).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
}
