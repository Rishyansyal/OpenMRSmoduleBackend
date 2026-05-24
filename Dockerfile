FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpenMRSmoduleBackend.csproj ./
RUN dotnet restore OpenMRSmoduleBackend.csproj

COPY . ./
RUN dotnet publish OpenMRSmoduleBackend.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "OpenMRSmoduleBackend.dll"]
