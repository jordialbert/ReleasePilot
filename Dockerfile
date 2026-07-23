FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS build
ARG APP_PROJECT
WORKDIR /src

COPY src/ReleaseManagement/ReleaseManagement.csproj src/ReleaseManagement/
COPY apps/ReleasePilot/Api/ReleasePilot.Api.csproj apps/ReleasePilot/Api/
COPY apps/ReleasePilot/Worker/ReleasePilot.Worker.csproj apps/ReleasePilot/Worker/
RUN dotnet restore "$APP_PROJECT"

COPY . .
RUN dotnet publish "$APP_PROJECT" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble AS final
ARG APP_DLL
ENV APP_DLL=$APP_DLL
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
