# Use the official .NET 9 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["App12.Api.csproj", "."]
RUN dotnet restore "App12.Api.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "App12.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App12.Api.csproj" -c Release -o /app/publish

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create tmp directory for SQLite database
RUN mkdir -p /tmp

ENTRYPOINT ["dotnet", "App12.Api.dll"]