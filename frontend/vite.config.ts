import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Trong container, dev server phải bind 0.0.0.0 thì port publish ra host mới nhận được kết nối.
    host: true,
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
        // Chạy bằng docker compose thì backend nằm ở service khác, không phải localhost của container.
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5080',
        changeOrigin: false,
      },
    },
  },
})
