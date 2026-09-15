import { defineConfig } from "vite"
import { fileURLToPath } from "node:url"
export default defineConfig({
  build: {
    target: "node24",
    lib: { entry: { server: "src/server.ts", index: "src/index.ts", demo: "examples/demo.ts" }, formats: ["es"], fileName: (_format, name) => `${name}.js` },
    rollupOptions: { external: (id) => id.startsWith("node:") },
    minify: false,
  },
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
})
