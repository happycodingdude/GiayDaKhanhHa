# Build image tách khỏi runtime image: image cuối chỉ có runtime ASP.NET, không kèm SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy trước phần mô tả project để restore được cache lại khi chỉ có mã nguồn thay đổi.
COPY Directory.Build.props ./
COPY src/ProductionManagement.Domain/*.csproj src/ProductionManagement.Domain/
COPY src/ProductionManagement.Application/*.csproj src/ProductionManagement.Application/
COPY src/ProductionManagement.Infrastructure/*.csproj src/ProductionManagement.Infrastructure/
COPY src/ProductionManagement.Api/*.csproj src/ProductionManagement.Api/
RUN dotnet restore src/ProductionManagement.Api/ProductionManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/ProductionManagement.Api/ProductionManagement.Api.csproj \
    -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
EXPOSE 5080
ENTRYPOINT ["dotnet", "ProductionManagement.Api.dll"]
