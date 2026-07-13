FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG PROJECT=host/CasinoShiz.Host/CasinoShiz.Host.csproj
ARG APP_DLL=CasinoShiz.Host.dll

COPY CasinoShiz.slnx Directory.Build.props ./
COPY framework/ framework/
COPY games/ games/
COPY host/ host/
COPY services/ services/

RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app/publish --no-restore --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ARG APP_DLL=CasinoShiz.Host.dll
ENV APP_DLL=$APP_DLL
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
