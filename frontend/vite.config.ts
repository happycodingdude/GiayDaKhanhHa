import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Không phải kiểu mount nào cũng phát sự kiện thay đổi file. Ở những nơi thiếu sự kiện đó,
    // watcher không bao giờ chạy, nên sửa file xong dev server vẫn trả bản transform đang cache
    // và HMR im lặng không làm gì. Polling tốn thêm chút CPU lúc rảnh và loại bỏ hẳn kiểu lỗi
    // này.
    watch: {
      usePolling: true,
      interval: 300,
    },
    // API được proxy dưới cùng một origin để cookie xác thực HttpOnly là cookie first-party và
    // không cần cấu hình CORS.
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: false,
      },
    },
  },
})
