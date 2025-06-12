FROM node:24 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:latest AS runner

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/berg-frontend/browser/ /usr/share/nginx/html/
