# Dev server: mã nguồn được mount từ host lúc chạy nên image chỉ cần chứa dependency.
FROM node:22-alpine
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

EXPOSE 5173
CMD ["npm", "run", "dev"]
