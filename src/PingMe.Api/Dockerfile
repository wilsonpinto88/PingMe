FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/PingMe.Api/PingMe.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Local docker-compose maps a fixed host port to 8080. A PaaS host (Render,
# Fly, etc.) instead injects the port to listen on via $PORT at container
# start, which a Dockerfile ENV cannot reference — only a shell-form command
# can expand it at runtime. This falls back to 8080 when PORT is unset, so
# docker-compose keeps working unchanged.
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ASPNETCORE_URLS=http://+:$PORT dotnet PingMe.Api.dll
