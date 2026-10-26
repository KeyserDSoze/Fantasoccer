FROM node:24.11.1-bookworm-slim AS sources
WORKDIR /app
COPY src/Fantasoccer.Applications/fantasoccer/package*.json ./
# Match the lockfile's peer-dependency resolution used by deploy-pages.yml.
RUN npm ci --legacy-peer-deps --no-audit --no-fund
COPY src/Fantasoccer.Applications/fantasoccer/ ./

FROM sources AS frontend-tests
COPY src/Fantasoccer.Applications/Fantasoccer.Application.Api/Mcp/ /Fantasoccer.Application.Api/Mcp/
COPY src/Fantasoccer.Applications/Fantasoccer.Application.Api/Fantasoccer.Application.Api.csproj /Fantasoccer.Application.Api/Fantasoccer.Application.Api.csproj
ENV EXPO_NO_TELEMETRY=1 CI=1
CMD ["bash", "-euc", "npm test\nnpx tsc --noEmit"]

FROM sources AS build
ARG EXPO_PUBLIC_API_URL=http://localhost:5080
ARG EXPO_PUBLIC_BLOB_URL=http://localhost:21000/migrationrestore
ARG EXPO_PUBLIC_MICROSOFT_CLIENT_ID
ARG EXPO_PUBLIC_WORKSPACE_ENABLED=false
ENV EXPO_PUBLIC_API_URL=$EXPO_PUBLIC_API_URL \
    EXPO_PUBLIC_BLOB_URL=$EXPO_PUBLIC_BLOB_URL \
    EXPO_PUBLIC_MICROSOFT_CLIENT_ID=$EXPO_PUBLIC_MICROSOFT_CLIENT_ID \
    EXPO_PUBLIC_WORKSPACE_ENABLED=$EXPO_PUBLIC_WORKSPACE_ENABLED \
    EXPO_NO_TELEMETRY=1 CI=1
RUN npx expo export --platform web

FROM nginx:1.28.0-alpine
COPY ops/local/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
