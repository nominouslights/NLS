import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Mirrors the "@/*" path map in tsconfig.json. Without it every `@/lib/...` import
    // in a test fails to resolve, since Vitest does not read tsconfig paths.
    // `import.meta.dirname`, not `__dirname` — this config is ESM, and @types/node
    // declares `__dirname` globally so TypeScript would not catch the mistake.
    alias: { "@": import.meta.dirname },
  },
  test: {
    environment: "jsdom",
    include: ["**/*.test.ts", "**/*.test.tsx"],
  },
});
