import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// One self-contained ES module (plus its CSS) that casting.js imports on demand.
export default defineConfig({
  plugins: [react()],
  define: { 'process.env.NODE_ENV': '"production"' },
  build: {
    outDir: '../wwwroot/topology',
    emptyOutDir: true,
    cssCodeSplit: false,
    lib: {
      entry: 'src/topology.jsx',
      formats: ['es'],
      fileName: () => 'topology.js',
      cssFileName: 'topology'
    }
  }
});
