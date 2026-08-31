/** Central API client. Wraps fetch, attaches the bearer token, unwraps the {success,data} envelope,
 *  and transparently refreshes an expired access token once. */

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
      const res = await fetch("/api/auth/refresh", {
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

  let url = path.startsWith("/api") ? path : `/api${path}`;
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
  const url = path.startsWith("/api") ? path : `/api${path}`;
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
