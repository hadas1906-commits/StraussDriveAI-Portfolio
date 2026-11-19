import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
const apiPort = process.env.PORTFOLIO_API_PORT ?? "5080";
if (!/^\d+$/.test(apiPort) || Number(apiPort) < 1024 || Number(apiPort) > 65535) throw new Error("Invalid local API port");
export default defineConfig({
  plugins: [react(), tailwindcss(), {name: "third-party-notices", generateBundle(){ this.emitFile({type:"asset",fileName:"THIRD_PARTY_NOTICES.md",source:readFileSync(fileURLToPath(new URL("../THIRD_PARTY_NOTICES.md",import.meta.url)),"utf8")});}}],
  server: {
    host: "127.0.0.1",
    proxy: {
      "/api": {
        target: `http://127.0.0.1:${apiPort}`,
        changeOrigin: true,
      },
    },
  },
  test: {
    include: ["src/**/*.test.{ts,tsx}"],
    environment: "jsdom",
    setupFiles: "./src/test-setup.ts",
  },
});
