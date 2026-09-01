import { create } from "zustand";
import { api, tokens } from "@/lib/api";
import type { AuthResponse, AuthUser, CompanyInfo, PlatformUserInfo } from "@/lib/types";

interface AuthState {
  /** Signed-in company user. Null when signed out or signed in as a platform operator. */
  user: AuthUser | null;
  /** The tenant the user belongs to — carries the licence countdown. */
  company: CompanyInfo | null;
  /** Signed-in EnterpriseAdmin. Mutually exclusive with `user`. */
  platformUser: PlatformUserInfo | null;
  loading: boolean;

  login: (login: string, password: string) => Promise<AuthResponse>;
  logout: () => Promise<void>;
  bootstrap: () => Promise<void>;
  can: (permission: string) => boolean;
}

export const useAuth = create<AuthState>((set, get) => ({
  user: null,
  company: null,
  platformUser: null,
  loading: true,

  login: async (login, password) => {
    const res = await api<AuthResponse>("/auth/login", { method: "POST", body: { login, password } });
    tokens.set(res.accessToken, res.refreshToken);
    set({ user: res.user, company: res.company, platformUser: res.platformUser, loading: false });
    return res;
  },

  logout: async () => {
    try { await api("/auth/logout", { method: "POST" }); } catch { /* the session is going away regardless */ }
    tokens.clear();
    set({ user: null, company: null, platformUser: null });
  },

  bootstrap: async () => {
    if (!tokens.access) { set({ loading: false }); return; }
    try {
      const me = await api<AuthResponse>("/auth/me");
      set({ user: me.user, company: me.company, platformUser: me.platformUser, loading: false });
    } catch {
      tokens.clear();
      set({ user: null, company: null, platformUser: null, loading: false });
    }
  },

  /** A platform operator holds no company permissions, so this is false for them by construction. */
  can: (permission) => get().user?.permissions.includes(permission) ?? false,
}));
