import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development, `npm run dev` serves the UI on http://localhost:5173 and forwards /api to the .NET API.
// `npm run build` writes the finished site into the API's wwwroot, so `dotnet run` serves everything.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080'
    }
  },
  build: {
    outDir: '../ShopFinder.Api/wwwroot',
    emptyOutDir: true
  }
});
