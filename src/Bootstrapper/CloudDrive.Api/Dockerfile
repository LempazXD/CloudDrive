FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/Bootstrapper/CloudDrive.Api/CloudDrive.Api.csproj", "src/Bootstrapper/CloudDrive.Api/"]
COPY ["src/Shared/Shared.Api/Shared.Api.csproj", "src/Shared/Shared.Api/"]
COPY ["src/Shared/Shared.Kernel/Shared.Kernel.csproj", "src/Shared/Shared.Kernel/"]
COPY ["src/Modules/Auth/Auth.Core/Auth.Core.csproj", "src/Modules/Auth/Auth.Core/"]
COPY ["src/Modules/Auth/Auth.Infrastructure/Auth.Infrastructure.csproj", "src/Modules/Auth/Auth.Infrastructure/"]
COPY ["src/Modules/Auth/Auth.Endpoints/Auth.Endpoints.csproj", "src/Modules/Auth/Auth.Endpoints/"]
RUN dotnet restore "src/Bootstrapper/CloudDrive.Api/CloudDrive.Api.csproj"
COPY . .
WORKDIR "/src/src/Bootstrapper/CloudDrive.Api"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CloudDrive.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudDrive.Api.dll"]
