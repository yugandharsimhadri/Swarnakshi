import { create } from "zustand";
import { api, tokens } from "@/lib/api";
import type { AuthResponse, AuthUser } from "@/lib/types";

interface AuthState {
  user: AuthUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  bootstrap: () => Promise<void>;
  can: (permission: string) => boolean;
}

export const useAuth = create<AuthState>((set, get) => ({
  user: null,
  loading: true,

  login: async (email, password) => {
    const res = await api<AuthResponse>("/auth/login", { method: "POST", body: { email, password } });
    tokens.set(res.accessToken, res.refreshToken);
    set({ user: res.user, loading: false });
  },

  logout: async () => {
    try { await api("/auth/logout", { method: "POST" }); } catch { /* ignore */ }
    tokens.clear();
    set({ user: null });
  },

  bootstrap: async () => {
    if (!tokens.access) { set({ loading: false }); return; }
    try {
      const user = await api<AuthUser>("/auth/me");
      set({ user, loading: false });
    } catch {
      tokens.clear();
      set({ user: null, loading: false });
    }
  },

  can: (permission) => get().user?.permissions.includes(permission) ?? false,
}));
