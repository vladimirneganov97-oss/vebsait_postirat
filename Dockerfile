# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY laundry_site.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV LAUNDRY_DB_PATH=/data/laundry.db

COPY --from=build /app/publish ./

RUN mkdir -p /data

EXPOSE 5000

ENTRYPOINT ["dotnet", "laundry_site.dll"]
