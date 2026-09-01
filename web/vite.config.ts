import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "node:path";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { "@": path.resolve(__dirname, "src") } },
  server: {
    // Overridable so the UAT suite can run the client and API on their own ports and never
    // attach to — or disturb — a developer's dev servers. Defaults are the normal dev ports.
    port: Number(process.env.SWARNAKSHI_WEB_PORT ?? 6050),
    strictPort: true,
    proxy: {
      "/api": {
        target: process.env.SWARNAKSHI_API_URL ?? "http://localhost:6051",
        changeOrigin: true,
      },
    },
  },
});
