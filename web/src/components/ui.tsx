import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from "react";
import { useLocation, useNavigate } from "react-router-dom";

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

/**
 * How far along a piece of work is. The number is shown as well as the bar — a bar alone is a
 * rough impression, and "62%" is what someone actually reports up the chain.
 */
export function ProgressBar({ percent, label }: { percent: number; label?: string }) {
  const value = Math.max(0, Math.min(100, Math.round(percent)));
  const tone = value >= 100 ? "bg-ok" : value === 0 ? "bg-border" : "bg-brand";
  return (
    <div className="w-full">
      <div className="mb-1 flex items-center justify-between text-xs text-text-dim">
        <span>{label ?? "Progress"}</span>
        <span className="font-medium text-text">{value}%</span>
      </div>
      <div
        className="h-2 w-full overflow-hidden rounded-full bg-border"
        role="progressbar"
        aria-valuenow={value}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={label ?? "Progress"}
      >
        <div className={`h-full rounded-full transition-[width] ${tone}`} style={{ width: `${value}%` }} />
      </div>
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

// text-base on phones is deliberate: iOS Safari auto-zooms the page whenever a focused field
// is under 16px. Drops back to text-sm from the sm breakpoint up.
const inputClass =
  "w-full rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-base outline-none focus:border-brand sm:text-sm";

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={inputClass} {...props} />;
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={inputClass} {...props} />;
}

/**
 * The five screens the bottom bar can already reach. Anywhere else in the app was arrived at by
 * tapping something, so it gets a back arrow — a phone user who has drilled three levels into a
 * villa should never have to guess their way out.
 */
const TAB_ROOTS = new Set(["/", "/projects", "/inventory", "/approvals", "/more"]);

export function PageHeader({ title, subtitle, action, back }: {
  title: string;
  subtitle?: ReactNode;
  action?: ReactNode;
  /** A route to go back to, or false to suppress the arrow. Defaults to browser history. */
  back?: string | false;
}) {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const showBack = back !== false && !TAB_ROOTS.has(pathname);

  return (
    <div className="px-1 pb-3 pt-1">
      {showBack && (
        <button
          type="button"
          onClick={() => (typeof back === "string" ? navigate(back) : navigate(-1))}
          className="-ml-1 mb-1 inline-flex min-h-11 items-center gap-1 pr-2 text-xs text-text-dim hover:text-text"
        >
          <span aria-hidden className="text-base leading-none">←</span> Back
        </button>
      )}
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <h1 className="truncate text-lg font-bold">{title}</h1>
          {subtitle && <div className="truncate text-xs text-text-dim">{subtitle}</div>}
        </div>
        {action}
      </div>
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

/** Placeholder cards shown while a list loads. */
export function SkeletonList({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="rounded-2xl border border-border bg-surface p-4">
          <div className="h-3.5 w-1/2 animate-pulse rounded bg-surface-2" />
          <div className="mt-2 h-3 w-1/3 animate-pulse rounded bg-surface-2" />
        </div>
      ))}
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

/** Wide variant of Sheet for multi-section master forms. Bottom sheet on phones, dialog on desktop. */
export function FormSheet({ open, onClose, title, subtitle, footer, children }: {
  open: boolean; onClose: () => void; title: string; subtitle?: string;
  footer?: ReactNode; children: ReactNode;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-4" role="dialog" aria-modal>
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative flex max-h-[92vh] w-full max-w-2xl flex-col rounded-t-3xl border border-border bg-surface sm:max-h-[88vh] sm:rounded-3xl">
        <div className="mx-auto mt-2 h-1 w-10 shrink-0 rounded-full bg-border sm:hidden" />
        <div className="flex shrink-0 items-start justify-between gap-3 border-b border-border px-4 py-3">
          <div className="min-w-0">
            <h2 className="truncate text-base font-bold">{title}</h2>
            {subtitle && <p className="truncate text-xs text-text-dim">{subtitle}</p>}
          </div>
          <button onClick={onClose} aria-label="Close"
            className="rounded-lg px-2 py-1 text-text-dim active:bg-surface-2">✕</button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">{children}</div>
        {footer && <div className="shrink-0 border-t border-border px-4 py-3 pb-6 sm:pb-3">{footer}</div>}
      </div>
    </div>
  );
}

/** Titled group of form fields. */
export function FormSection({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <section className="mb-5 last:mb-0">
      <div className="mb-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-text-dim">{title}</h3>
        {hint && <p className="mt-0.5 text-xs text-text-dim">{hint}</p>}
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">{children}</div>
    </section>
  );
}

/** Horizontally scrollable table shell — the page body must never scroll sideways. */
export function TableWrap({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-border bg-surface">
      <table className="w-full min-w-[54rem] border-collapse text-sm">{children}</table>
    </div>
  );
}

export function Th({ children, className = "" }: { children?: ReactNode; className?: string }) {
  return (
    <th className={`border-b border-border px-3 py-2.5 text-left text-xs font-semibold text-text-dim ${className}`}>
      {children}
    </th>
  );
}

export function Td({ children, className = "" }: { children?: ReactNode; className?: string }) {
  return <td className={`border-b border-border px-3 py-2.5 align-middle ${className}`}>{children}</td>;
}

/** Label/value pair for read-only detail views. */
export function DetailRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 border-b border-border py-2 last:border-0">
      <span className="shrink-0 text-xs text-text-dim">{label}</span>
      <span className="min-w-0 break-words text-right text-sm">{value ?? "—"}</span>
    </div>
  );
}
