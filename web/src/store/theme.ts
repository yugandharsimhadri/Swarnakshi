import { create } from "zustand";

type Theme = "light" | "dark";

function initial(): Theme {
  const saved = localStorage.getItem("swk.theme") as Theme | null;
  if (saved) return saved;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function apply(t: Theme) {
  document.documentElement.classList.toggle("dark", t === "dark");
  localStorage.setItem("swk.theme", t);
}

interface ThemeState {
  theme: Theme;
  toggle: () => void;
}

export const useTheme = create<ThemeState>((set, get) => {
  apply(initial());
  return {
    theme: initial(),
    toggle: () => {
      const next: Theme = get().theme === "dark" ? "light" : "dark";
      apply(next);
      set({ theme: next });
    },
  };
});
