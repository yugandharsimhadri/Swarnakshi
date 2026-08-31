import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from "react";

export function Card({ children, className = "", onClick }: { children: ReactNode; className?: string; onClick?: () => void }) {
  return (
    <div
      onClick={onClick}
      className={`rounded-2xl border border-border bg-surface p-4 ${onClick ? "active:scale-[0.99] transition" : ""} ${className}`}
    >
      {children}
    </div>
  );
}

export function StatCard({ label, value, sub, tone }: { label: string; value: string; sub?: string; tone?: "ok" | "warn" | "danger" }) {
  const toneClass = tone === "ok" ? "text-ok" : tone === "warn" ? "text-warn" : tone === "danger" ? "text-danger" : "text-text";
  return (
    <Card className="min-w-0">
      <div className="text-xs text-text-dim truncate">{label}</div>
      <div className={`mt-1 text-xl font-semibold tabular-nums truncate ${toneClass}`}>{value}</div>
      {sub && <div className="mt-0.5 text-xs text-text-dim truncate">{sub}</div>}
    </Card>
  );
}

const chipTones: Record<string, string> = {
  neutral: "bg-surface-2 text-text-dim",
  ok: "bg-ok/15 text-ok",
  warn: "bg-warn/15 text-warn",
  danger: "bg-danger/15 text-danger",
  brand: "bg-brand/15 text-brand-ink",
};

export function Chip({ children, tone = "neutral" }: { children: ReactNode; tone?: keyof typeof chipTones }) {
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${chipTones[tone]}`}>{children}</span>;
}

export function Button({
  variant = "primary",
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "ghost" | "danger" }) {
  const base = "inline-flex items-center justify-center gap-2 rounded-xl px-4 py-2.5 text-sm font-semibold transition disabled:opacity-50";
  const variants = {
    primary: "bg-brand text-white active:brightness-95",
    ghost: "bg-surface-2 text-text active:brightness-95",
    danger: "bg-danger text-white active:brightness-95",
  };
  return <button className={`${base} ${variants[variant]} ${className}`} {...props} />;
}

export function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-text-dim">{label}</span>
      {children}
      {error && <span className="mt-1 block text-xs text-danger">{error}</span>}
    </label>
  );
}

const inputClass =
  "w-full rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-sm outline-none focus:border-brand";

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={inputClass} {...props} />;
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={inputClass} {...props} />;
}

export function PageHeader({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 px-1 pb-3 pt-1">
      <h1 className="text-lg font-bold">{title}</h1>
      {action}
    </div>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="rounded-2xl border border-dashed border-border p-8 text-center">
      <div className="text-sm font-medium">{title}</div>
      {hint && <div className="mt-1 text-xs text-text-dim">{hint}</div>}
    </div>
  );
}

export function Spinner() {
  return (
    <div className="flex justify-center py-10">
      <div className="h-6 w-6 animate-spin rounded-full border-2 border-border border-t-brand" />
    </div>
  );
}

export function Sheet({ open, onClose, title, children }: { open: boolean; onClose: () => void; title: string; children: ReactNode }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center" role="dialog" aria-modal>
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative w-full max-w-md rounded-t-3xl border border-border bg-surface p-4 pb-8 sm:rounded-3xl">
        <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-border sm:hidden" />
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-base font-bold">{title}</h2>
          <button onClick={onClose} className="rounded-lg px-2 py-1 text-text-dim active:bg-surface-2">✕</button>
        </div>
        <div className="max-h-[70vh] overflow-y-auto">{children}</div>
      </div>
    </div>
  );
}

export function Confirm({
  open, title, body, confirmLabel = "Confirm", danger, onConfirm, onCancel,
}: {
  open: boolean; title: string; body?: ReactNode; confirmLabel?: string; danger?: boolean;
  onConfirm: () => void; onCancel: () => void;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4" role="dialog" aria-modal>
      <div className="absolute inset-0 bg-black/50" onClick={onCancel} />
      <div className="relative w-full max-w-xs rounded-2xl border border-border bg-surface p-4">
        <div className="text-base font-bold">{title}</div>
        {body && <div className="mt-1 text-sm text-text-dim">{body}</div>}
        <div className="mt-4 flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={onCancel}>Cancel</Button>
          <Button variant={danger ? "danger" : "primary"} className="flex-1" onClick={onConfirm}>{confirmLabel}</Button>
        </div>
      </div>
    </div>
  );
}

export function ErrorText({ error }: { error: { message: string; errors: string[] } | null }) {
  if (!error) return null;
  return (
    <div className="rounded-xl bg-danger/10 px-3 py-2 text-xs text-danger">
      <div className="font-medium">{error.message}</div>
      {error.errors.length > 0 && (
        <ul className="mt-1 list-inside list-disc">
          {error.errors.map((e, i) => <li key={i}>{e}</li>)}
        </ul>
      )}
    </div>
  );
}
