FROM node:24-bookworm-slim AS build
WORKDIR /app
COPY Projects/TikTok-Live-Turnbased/app/package*.json ./
RUN npm ci
COPY Projects/TikTok-Live-Turnbased/app/ ./
RUN npm run build && npm test

FROM node:24-bookworm-slim
ENV NODE_ENV=production HOST=127.0.0.1 PORT=3400 ROOMS_PATH=/data/rooms TRUSTED_PROXY=127.0.0.1
WORKDIR /app
COPY --from=build /app/dist ./dist
COPY --from=build /app/src ./src
COPY --from=build /app/vendor ./vendor
COPY --from=build /app/package*.json ./
COPY --from=build /app/node_modules ./node_modules
RUN mkdir /data && chown node:node /data
USER node
HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=6 CMD node -e "fetch('http://127.0.0.1:'+process.env.PORT+'/health').then(async r=>{const s=await r.json();if(!r.ok||s.mode!=='rooms')process.exit(1)}).catch(()=>process.exit(1))"
CMD ["node", "--import", "tsx", "src/server.ts"]
