/** Central API client. Wraps fetch, attaches the bearer token, unwraps the {success,data} envelope,
 *  and transparently refreshes an expired access token once. */

/**
 * Where the API lives, decided when the bundle is built.
 *
 * Empty — the default — means "the host that served this page", which is right in development
 * (Vite proxies /api) and right when the API serves the built UI out of its own wwwroot.
 *
 * Set `VITE_API_BASE_URL` to an absolute origin when the two are hosted apart: a UI on Cloudflare
 * Pages and an API on IIS behind a tunnel have different hostnames, and a relative /api would ask
 * the CDN for an endpoint it has never heard of. That origin must also appear in the API's
 * `Cors:Origins`, because the call is then genuinely cross-origin.
 */
const API_ORIGIN = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/+$/, "");

/** Absolute or relative, depending on how the bundle was built. Always ends up rooted at /api. */
function apiUrl(path: string): string {
  return API_ORIGIN + (path.startsWith("/api") ? path : `/api${path}`);
}

export interface ApiError {
  message: string;
  errors: string[];
  status: number;
}

interface Envelope<T> {
  success: boolean;
  message: string | null;
  data: T | null;
  errors: string[];
}

const ACCESS_KEY = "swk.access";
const REFRESH_KEY = "swk.refresh";

export const tokens = {
  get access() { return localStorage.getItem(ACCESS_KEY); },
  get refresh() { return localStorage.getItem(REFRESH_KEY); },
  set(access: string, refresh: string) {
    localStorage.setItem(ACCESS_KEY, access);
    localStorage.setItem(REFRESH_KEY, refresh);
  },
  clear() {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
};

let refreshing: Promise<boolean> | null = null;

async function tryRefresh(): Promise<boolean> {
  if (!tokens.refresh) return false;
  refreshing ??= (async () => {
    try {
      const res = await fetch(apiUrl("/auth/refresh"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: tokens.refresh }),
      });
      const body = (await res.json()) as Envelope<{ accessToken: string; refreshToken: string }>;
      if (!res.ok || !body.success || !body.data) return false;
      tokens.set(body.data.accessToken, body.data.refreshToken);
      return true;
    } catch {
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

export async function api<T>(
  path: string,
  opts: { method?: string; body?: unknown; query?: Record<string, unknown>; retry?: boolean } = {},
): Promise<T> {
  const { method = "GET", body, query, retry = true } = opts;

  let url = apiUrl(path);
  if (query) {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== "") qs.set(k, String(v));
    }
    const s = qs.toString();
    if (s) url += `?${s}`;
  }

  const headers: Record<string, string> = {};
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (tokens.access) headers.Authorization = `Bearer ${tokens.access}`;

  const res = await fetch(url, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401 && retry && (await tryRefresh())) {
    return api<T>(path, { ...opts, retry: false });
  }

  let payload: Envelope<T> | null = null;
  try { payload = (await res.json()) as Envelope<T>; } catch { /* no body */ }

  if (!res.ok || !payload?.success) {
    const err: ApiError = {
      message: payload?.message ?? `Request failed (${res.status})`,
      errors: payload?.errors ?? [],
      status: res.status,
    };
    throw err;
  }
  return payload.data as T;
}

/** Multipart upload — the JSON `api()` helper can't send FormData. */
export async function apiUpload<T>(path: string, form: FormData): Promise<T> {
  const url = apiUrl(path);
  const headers: Record<string, string> = {};
  if (tokens.access) headers.Authorization = `Bearer ${tokens.access}`;

  let res = await fetch(url, { method: "POST", headers, body: form });
  if (res.status === 401 && (await tryRefresh())) {
    if (tokens.access) headers.Authorization = `Bearer ${tokens.access}`;
    res = await fetch(url, { method: "POST", headers, body: form });
  }

  const payload = (await res.json().catch(() => null)) as
    | { success: boolean; message: string | null; data: T | null; errors: string[] }
    | null;
  if (!res.ok || !payload?.success) {
    throw {
      message: payload?.message ?? `Upload failed (${res.status})`,
      errors: payload?.errors ?? [],
      status: res.status,
    } satisfies ApiError;
  }
  return payload.data as T;
}
