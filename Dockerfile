# Runs the server in Streamable HTTP mode on port 8080 (MCP endpoint: /mcp, health: /healthz).
# docker build -t game-intel-mcp .
# docker run -p 8080:8080 -e Mcp__ApiKey=change-me game-intel-mcp

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached between code changes.
COPY global.json Directory.Build.props Directory.Packages.props GameIntel.slnx ./
COPY src/GameIntel.Core/GameIntel.Core.csproj src/GameIntel.Core/
COPY src/GameIntel.Providers.Dota/GameIntel.Providers.Dota.csproj src/GameIntel.Providers.Dota/
COPY src/GameIntel.Providers.Poe2/GameIntel.Providers.Poe2.csproj src/GameIntel.Providers.Poe2/
COPY src/GameIntel.McpServer/GameIntel.McpServer.csproj src/GameIntel.McpServer/
RUN dotnet restore src/GameIntel.McpServer/GameIntel.McpServer.csproj

COPY . .
RUN dotnet publish src/GameIntel.McpServer/GameIntel.McpServer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# The base image ships a non-root 'app' user; don't run as root.
USER app

ENTRYPOINT ["dotnet", "game-intel-mcp.dll", "--http"]
