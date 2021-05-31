
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["FanoutAPIV2.csproj", "Api/"]
RUN dotnet restore "Api/FanoutAPIV2.csproj"
COPY . ./Api
WORKDIR "/src/Api"
RUN dotnet build "FanoutAPIV2.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FanoutAPIV2.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FanoutAPIV2.dll"]