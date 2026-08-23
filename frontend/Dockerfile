FROM node:22-alpine AS base
WORKDIR /app
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

FROM base AS development
COPY . .
EXPOSE 3000
CMD ["pnpm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "3000"]

FROM base AS build
COPY . .
RUN pnpm run build

FROM nginx:1.27-alpine AS production
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
