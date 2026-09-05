# Stage 1: Build stage with .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy project files and restore dependencies
COPY ["src/NexaOps.Domain/NexaOps.Domain.csproj", "src/NexaOps.Domain/"]
COPY ["src/NexaOps.Application/NexaOps.Application.csproj", "src/NexaOps.Application/"]
COPY ["src/NexaOps.Infrastructure/NexaOps.Infrastructure.csproj", "src/NexaOps.Infrastructure/"]
COPY ["src/NexaOps.Api/NexaOps.Api.csproj", "src/NexaOps.Api/"]

RUN dotnet restore "src/NexaOps.Api/NexaOps.Api.csproj"

# Copy full source and publish
COPY src/ src/
WORKDIR /app/src/NexaOps.Api
RUN dotnet publish "NexaOps.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Lightweight runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NexaOps.Api.dll"]
