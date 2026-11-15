# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution file and project references for restore layer caching
COPY CriaCerto.slnx ./
COPY src/BuildingBlocks/CriaCerto.BuildingBlocks.Abstractions/CriaCerto.BuildingBlocks.Abstractions.csproj src/BuildingBlocks/CriaCerto.BuildingBlocks.Abstractions/
COPY src/BuildingBlocks/CriaCerto.BuildingBlocks.Application/CriaCerto.BuildingBlocks.Application.csproj src/BuildingBlocks/CriaCerto.BuildingBlocks.Application/
COPY src/Modules/Analytics/CriaCerto.Modules.Analytics.Application/CriaCerto.Modules.Analytics.Application.csproj src/Modules/Analytics/CriaCerto.Modules.Analytics.Application/
COPY src/Modules/Backoffice/CriaCerto.Modules.Backoffice.Application/CriaCerto.Modules.Backoffice.Application.csproj src/Modules/Backoffice/CriaCerto.Modules.Backoffice.Application/
COPY src/Modules/Breeding/CriaCerto.Modules.Breeding.Application/CriaCerto.Modules.Breeding.Application.csproj src/Modules/Breeding/CriaCerto.Modules.Breeding.Application/
COPY src/Modules/Calving/CriaCerto.Modules.Calving.Application/CriaCerto.Modules.Calving.Application.csproj src/Modules/Calving/CriaCerto.Modules.Calving.Application/
COPY src/Modules/Growth/CriaCerto.Modules.Growth.Application/CriaCerto.Modules.Growth.Application.csproj src/Modules/Growth/CriaCerto.Modules.Growth.Application/
COPY src/Modules/Nutrition/CriaCerto.Modules.Nutrition.Application/CriaCerto.Modules.Nutrition.Application.csproj src/Modules/Nutrition/CriaCerto.Modules.Nutrition.Application/
COPY src/Modules/Referral/CriaCerto.Modules.Referral.Application/CriaCerto.Modules.Referral.Application.csproj src/Modules/Referral/CriaCerto.Modules.Referral.Application/
COPY src/Modules/Sanitary/CriaCerto.Modules.Sanitary.Application/CriaCerto.Modules.Sanitary.Application.csproj src/Modules/Sanitary/CriaCerto.Modules.Sanitary.Application/
COPY src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/CriaCerto.Modules.Tenancy.Application.csproj src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/
COPY src/Web/CriaCerto.Shared.UI/CriaCerto.Shared.UI.csproj src/Web/CriaCerto.Shared.UI/
COPY src/Web/CriaCerto.Web/CriaCerto.Web/CriaCerto.Web.csproj src/Web/CriaCerto.Web/CriaCerto.Web/
COPY src/Web/CriaCerto.Web/CriaCerto.Web.Client/CriaCerto.Web.Client.csproj src/Web/CriaCerto.Web/CriaCerto.Web.Client/

RUN dotnet restore src/Web/CriaCerto.Web/CriaCerto.Web/CriaCerto.Web.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/Web/CriaCerto.Web/CriaCerto.Web/CriaCerto.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CriaCerto.Web.dll"]
