import path from "path";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    // Dev server only (see Dockerfile). Vite's default anti-DNS-rebinding Host check
    // only allows localhost/127.0.0.1, which rejects requests carrying the
    // docker-compose service hostname ("web") — needed so the Playwright e2e suite can
    // run from a container on the same compose network.
    allowedHosts: true,
  },
});
