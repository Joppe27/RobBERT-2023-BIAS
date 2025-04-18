FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RobBERT-2023-BIAS.Server/RobBERT-2023-BIAS.Server.csproj", "RobBERT-2023-BIAS.Server/"]
RUN dotnet restore "RobBERT-2023-BIAS.Server/RobBERT-2023-BIAS.Server.csproj"
COPY . .
WORKDIR "/src/RobBERT-2023-BIAS.Server"
RUN dotnet build "RobBERT-2023-BIAS.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RobBERT-2023-BIAS.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RobBERT-2023-BIAS.Server.dll"]
