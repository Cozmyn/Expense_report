FROM node:22-bookworm-slim AS ui
WORKDIR /src/ui
COPY ui/package*.json ./
RUN npm ci
COPY ui/ ./
COPY deploy/check-release.mjs /src/deploy/check-release.mjs
RUN npm test && npm run build
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY server/ server/
COPY tests/ tests/
RUN dotnet restore tests/Decont.Tests.csproj && dotnet test tests/Decont.Tests.csproj -c Release
COPY --from=ui /src/server/wwwroot/ server/wwwroot/
RUN dotnet publish server/Decont.Api.csproj -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
USER root
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core tesseract-ocr tesseract-ocr-ron tesseract-ocr-eng tesseract-ocr-osd poppler-utils imagemagick libheif-examples && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir -p /app/.data && chown -R app:app /app
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet","Decont.Api.dll"]

