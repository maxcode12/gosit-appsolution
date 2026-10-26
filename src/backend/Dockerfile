# Build context is src/backend:  docker build -t gosit-api src/backend
# (in Azure: az acr build --registry <name> --image gosit-api:<tag> src/backend)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so the package restore layer is cached until a dependency changes.
COPY GoSit.Domain/GoSit.Domain.csproj GoSit.Domain/
COPY GoSit.Application/GoSit.Application.csproj GoSit.Application/
COPY GoSit.Infrastructure/GoSit.Infrastructure.csproj GoSit.Infrastructure/
COPY GoSit.Api/GoSit.Api.csproj GoSit.Api/
RUN dotnet restore GoSit.Api/GoSit.Api.csproj

COPY . .
# No --no-restore: if a stray obj/ folder from a developer's machine ever reaches the build
# (its package paths point at Windows), a fresh restore replaces it instead of failing.
RUN dotnet publish GoSit.Api/GoSit.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
# The platform's front door ends TLS and forwards plain HTTP to the container. Without this
# the app cannot tell the original request was HTTPS (so HTTPS redirection loops) and sees
# the proxy's address instead of the caller's, which would put every visitor in one
# rate-limit bucket.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# The base image ships a non-root user; the app has no reason to run as root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GoSit.Api.dll"]
