# syntax=docker/dockerfile:1.6

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DotGame/DotGameAvalonia.csproj
RUN dotnet publish DotGame/DotGameAvalonia.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS runtime
ENV DEBIAN_FRONTEND=noninteractive \
    DISPLAY=:1
RUN apt-get update && apt-get install -y --no-install-recommends \
    xvfb \
    x11vnc \
    fluxbox \
    supervisor \
    novnc \
    websockify \
    curl \
    libgtk-3-0 \
    libnss3 \
    libatspi2.0-0 \
    libx11-xcb1 \
    libxtst6 \
    libxrender1 \
    libasound2 \
    libgbm1 \
 && rm -rf /var/lib/apt/lists/*

COPY cloud/gcp-vnc/supervisord.conf /etc/supervisor/conf.d/supervisord.conf
COPY cloud/gcp-vnc/startup.sh /opt/startup.sh
RUN chmod +x /opt/startup.sh

COPY --from=build /app/publish /opt/app
WORKDIR /opt/app

EXPOSE 6080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -f http://localhost:6080 || exit 1

CMD ["/opt/startup.sh"]
