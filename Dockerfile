# Stage 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy nuget.config if it exists in the root
COPY nuget.config ./

# Copy csproj files for all projects to cache the restore layer
COPY src/DespensaInteligente.Domain/DespensaInteligente.Domain.csproj src/DespensaInteligente.Domain/
COPY src/DespensaInteligente.Application/DespensaInteligente.Application.csproj src/DespensaInteligente.Application/
COPY src/DespensaInteligente.Infrastructure/DespensaInteligente.Infrastructure.csproj src/DespensaInteligente.Infrastructure/
COPY src/DespensaInteligente.Api/DespensaInteligente.Api.csproj src/DespensaInteligente.Api/

# Restore NuGet packages
RUN dotnet restore src/DespensaInteligente.Api/DespensaInteligente.Api.csproj

# Copy the rest of the source code
COPY src/ src/

# Build and publish the API project
RUN dotnet publish src/DespensaInteligente.Api/DespensaInteligente.Api.csproj -c Release -o /out --no-restore

# Stage 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published files from build stage
COPY --from=build /out ./

# Create local storage folders and configure permissions
RUN mkdir -p /app/storage /app/logs && chmod -R 777 /app/storage /app/logs

# Define port environment variables (Render exposes PORT)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run the API, dynamically binding to the port provided by Render (defaulting to 8080 if not set)
CMD ["sh", "-c", "dotnet DespensaInteligente.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
