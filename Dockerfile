FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpenMRSmoduleBackend.csproj ./
RUN dotnet restore OpenMRSmoduleBackend.csproj

COPY . ./
RUN dotnet publish OpenMRSmoduleBackend.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: draai als non-root gebruiker om privilege escalation te voorkomen.
# UID/GID 1001 is buiten het root-bereik (0-999) en heeft geen speciale rechten.
RUN addgroup --system --gid 1001 app \
 && adduser  --system --uid 1001 --gid 1001 --no-create-home app

COPY --from=build /app ./

# Eigenaar van de app-bestanden = non-root user
RUN chown -R app:app /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "OpenMRSmoduleBackend.dll"]

