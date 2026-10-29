FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Portfolio.Api/Portfolio.Api.csproj backend/Portfolio.Api/
RUN dotnet restore backend/Portfolio.Api/Portfolio.Api.csproj
COPY backend/Portfolio.Api/ backend/Portfolio.Api/
RUN dotnet publish backend/Portfolio.Api/Portfolio.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The SQLite database and uploads are written here; the app runs as a non-root user.
RUN mkdir -p App_Data && chown $APP_UID App_Data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ReverseProxy__Enabled=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "Portfolio.Api.dll"]
