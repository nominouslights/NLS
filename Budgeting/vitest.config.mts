import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Mirrors the "@/*" path map in tsconfig.json. Without it every `@/lib/...` import
    // in a test fails to resolve, since Vitest does not read tsconfig paths.
    // `import.meta.dirname`, not `__dirname`: this file is ESM (.mts). `__dirname` does not
    // exist in an ES module, and @types/node declares it globally, so TypeScript would not
    // have caught the ReferenceError — every `@/lib/...` import would fail to resolve.
    alias: { "@": import.meta.dirname },
  },
  test: {
    environment: "jsdom",
    include: ["**/*.test.ts", "**/*.test.tsx"],
  },
});
