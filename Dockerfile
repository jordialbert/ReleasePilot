FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS build
ARG APP_PROJECT
WORKDIR /src

COPY ReleasePilot.sln ./
COPY src/ReleaseManagement/Domain/ReleaseManagement.Domain.csproj src/ReleaseManagement/Domain/
COPY src/ReleaseManagement/Application/ReleaseManagement.Application.csproj src/ReleaseManagement/Application/
COPY src/ReleaseManagement/Infrastructure/ReleaseManagement.Infrastructure.csproj src/ReleaseManagement/Infrastructure/
COPY apps/ReleasePilot/Api/ReleasePilot.Api.csproj apps/ReleasePilot/Api/
COPY apps/ReleasePilot/Worker/ReleasePilot.Worker.csproj apps/ReleasePilot/Worker/
COPY tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj tests/ReleaseManagement.Domain.Tests/
COPY tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj tests/ReleasePilot.IntegrationTests/
COPY tests/ReleasePilot.EndToEndTests/ReleasePilot.EndToEndTests.csproj tests/ReleasePilot.EndToEndTests/
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
