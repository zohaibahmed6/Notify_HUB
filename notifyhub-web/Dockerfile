FROM node:20-alpine

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY . .

EXPOSE 5173

# Dev server, not a production build — chosen for 3-day-scope simplicity (documented
# limitation in STATUS.md / ADRs). --host is required so the container's Vite dev
# server is reachable from outside the container network.
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "5173"]
