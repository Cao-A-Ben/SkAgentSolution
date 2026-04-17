import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const hostTarget = process.env.SKAGENT_HOST ?? "http://127.0.0.1:5192";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 4179,
    proxy: {
      "/api": {
        target: hostTarget,
        changeOrigin: true,
      },
    },
  },
});
