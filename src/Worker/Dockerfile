FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore scheduler-core.sln
RUN dotnet publish src/Worker/Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "Worker.dll"]
