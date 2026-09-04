import { Component, type ErrorInfo, type ReactNode } from "react";

/**
 * The last line of defence against a white screen.
 *
 * Without one of these, a single render error unmounts the whole tree and the person is left
 * looking at a blank page with nothing to click. On a phone, on a site, with no idea what happened,
 * that is indistinguishable from the app being gone — and the natural response is to assume the
 * day's entries were lost.
 *
 * So: say plainly that something broke, say that saved work is safe, and give the two ways out.
 * The error text is kept behind a disclosure because the person who needs it is whoever they ring,
 * not them.
 */
interface Props { children: ReactNode }
interface State { error: Error | null }

export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // The browser console is the only log this app has on a user's phone. Keep the component
    // stack with it — a stack trace alone rarely says which screen was on show.
    console.error("Unhandled render error:", error, info.componentStack);
  }

  render() {
    const { error } = this.state;
    if (!error) return this.props.children;

    return (
      <div className="flex min-h-dvh items-center justify-center bg-bg p-6">
        <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-6 text-center">
          <h1 className="text-lg font-semibold">Something went wrong on this screen</h1>
          <p className="mt-2 text-sm text-text-dim">
            Nothing you had already saved is affected. Try this screen again, or go back to the
            start.
          </p>

          <div className="mt-5 flex flex-col gap-2">
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="min-h-11 rounded-xl bg-brand px-4 text-sm font-semibold text-brand-contrast"
            >
              Try again
            </button>
            <button
              type="button"
              onClick={() => { window.location.href = "/"; }}
              className="min-h-11 rounded-xl border border-border px-4 text-sm font-medium"
            >
              Go to the home screen
            </button>
          </div>

          <details className="mt-5 text-left">
            <summary className="cursor-pointer text-xs text-text-dim">
              Details to report
            </summary>
            <pre className="mt-2 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-surface-2 p-3 text-[11px] text-text-dim">
              {error.message}
            </pre>
          </details>
        </div>
      </div>
    );
  }
}
