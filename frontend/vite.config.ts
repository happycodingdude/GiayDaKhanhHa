import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // The API is proxied under the same origin so the HttpOnly authentication cookie is a
    // first-party cookie and no CORS configuration is needed.
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: false,
      },
    },
  },
})
