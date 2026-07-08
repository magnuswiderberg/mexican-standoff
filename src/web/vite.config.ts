import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev: Vite serves the SPA with HMR and proxies the SignalR hub (WebSockets)
// to the .NET server. Prod: `npm run build` emits straight into the server's
// wwwroot so one Web App serves everything (docs/tech-stack.md).
export default defineConfig({
  plugins: [react()],
  server: {
    host: true, // reachable from phones on the party wifi
    proxy: {
      '/hub': { target: 'http://localhost:5068', ws: true },
      '/health': { target: 'http://localhost:5068' },
    },
  },
  build: {
    outDir: '../MexicanStandoff.Server/wwwroot',
    emptyOutDir: true,
  },
})
