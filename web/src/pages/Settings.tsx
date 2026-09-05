import { useEffect, useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money } from "@/lib/format";
import { Button, Card, ErrorText, Field, Input, PageHeader, Spinner } from "@/components/ui";

interface ApprovalSettings {
  autoApproveLimit: number;
}

/**
 * The owner's own settings. One rule so far, and it is the one that decides how much of the day
 * passes across their desk: everything waits for them unless it is under this amount.
 */
export default function Settings() {
  const { data, loading, error, reload } = useAsync(() => api<ApprovalSettings>("/settings/approvals"), []);
  const [limit, setLimit] = useState("");
  const [saveErr, setSaveErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  // Seed the box from the server once it answers, and again after a save, so what is shown is
  // always what is stored rather than what was typed.
  useEffect(() => {
    if (data) setLimit(String(data.autoApproveLimit));
  }, [data]);

  const parsed = Number(limit);
  const valid = limit.trim() !== "" && Number.isFinite(parsed) && parsed >= 0;
  const changed = valid && data != null && parsed !== data.autoApproveLimit;

  async function save() {
    setBusy(true); setSaveErr(null); setSaved(false);
    try {
      await api("/settings/approvals", { method: "PUT", body: { autoApproveLimit: parsed } });
      setSaved(true);
      reload();
    } catch (e) {
      setSaveErr(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Settings" subtitle="Approvals" back="/more" />

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        <>
          <Card className="space-y-3">
            <div>
              <div className="text-sm font-semibold">Approve small amounts automatically</div>
              <p className="mt-1 text-xs leading-relaxed text-text-dim">
                Purchases, expenses and payments below this amount post straight away. Anything at or
                above it waits for you in the Approval Center.
              </p>
            </div>

            <Field label="Auto-approve limit (₹)">
              <Input
                inputMode="decimal"
                value={limit}
                onChange={(e) => { setLimit(e.target.value); setSaved(false); }}
                placeholder="0"
              />
            </Field>

            {/* Says what the number means in words, because "0" is the setting most likely to be
                read as "no limit" when it means the exact opposite. */}
            <div className="rounded-xl bg-brand/10 px-3 py-2 text-xs leading-relaxed">
              {!valid ? (
                <span className="text-danger">Enter an amount. Use 0 to send everything for approval.</span>
              ) : parsed === 0 ? (
                <>Everything goes to you for approval — no exceptions.</>
              ) : (
                <>
                  A {money(parsed - 1)} expense posts on its own. A {money(parsed)} one comes to you,
                  and so does anything larger.
                </>
              )}
            </div>

            <ErrorText error={saveErr} />
            <div className="flex items-center gap-3">
              <Button onClick={save} disabled={busy || !changed}>Save</Button>
              {saved && !changed && <span className="text-xs text-ok">Saved.</span>}
            </div>
          </Card>

          <Card className="space-y-2">
            <div className="text-sm font-semibold">What always needs approval</div>
            <ul className="space-y-1 text-xs leading-relaxed text-text-dim">
              <li>· Stock adjustments — they have no amount to compare, and write stock off against
                  no document.</li>
              <li>· Anything whose value cannot be worked out when it is raised.</li>
            </ul>
          </Card>
        </>
      )}
    </div>
  );
}
