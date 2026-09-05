import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "node:path";

/**
 * Starts the DNS lookup, the TCP connection and the TLS handshake to the API while the browser is
 * still parsing JavaScript, instead of after the first fetch is issued.
 *
 * It only matters — but it matters a lot — in the split deployment, where the UI comes from
 * Cloudflare's edge and the API from a tunnel to the office server: those are different origins, so
 * none of that setup is shared, and it lands squarely in front of the first screen that needs data.
 * Injected only when VITE_API_BASE_URL is set, because in the single-service build the API is the
 * same origin and the hint would be noise.
 */
function preconnectApi(apiBaseUrl: string | undefined): Plugin {
  return {
    name: "swarnakshi-preconnect-api",
    transformIndexHtml() {
      if (!apiBaseUrl) return [];
      let origin: string;
      try {
        origin = new URL(apiBaseUrl).origin;
      } catch {
        // A relative value is same-origin, which needs no hint. Never fail the build over it.
        return [];
      }
      return [
        { tag: "link", attrs: { rel: "preconnect", href: origin, crossorigin: "" }, injectTo: "head-prepend" },
        { tag: "link", attrs: { rel: "dns-prefetch", href: origin }, injectTo: "head-prepend" },
      ];
    },
  };
}

export default defineConfig(({ mode }) => {
  // Read from the real environment rather than loadEnv, because that is how the deployment scripts
  // pass it (VITE_API_BASE_URL=... npm run build).
  const apiBaseUrl = process.env.VITE_API_BASE_URL;

  return {
    plugins: [react(), tailwindcss(), preconnectApi(apiBaseUrl)],
    resolve: { alias: { "@": path.resolve(__dirname, "src") } },
    build: {
      // React, the router and the store change when a dependency is upgraded — a few times a year.
      // The application changes every deployment. Splitting them means a routine deploy re-downloads
      // only what was actually rewritten, and the vendor half stays in the browser's cache.
      rollupOptions: {
        output: {
          // By path rather than by package name: naming the packages misses the files they are
          // actually made of — react-dom's implementation is reached through react-dom/client, so
          // listing "react-dom" left the largest single file in the app's own chunk.
          manualChunks: (id) => (id.includes("node_modules") ? "vendor" : undefined),
        },
      },
      // The lazy route chunks are small by design; this only silences the advisory for the vendor
      // chunk, which is as small as React itself allows.
      chunkSizeWarningLimit: 250,
      sourcemap: mode !== "production",
    },
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
  };
});
