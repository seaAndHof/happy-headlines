# One Dockerfile for all .NET services; compose passes the project name as SERVICE.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE
WORKDIR /src
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/${SERVICE}/${SERVICE}.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SERVICE
ENV SERVICE=${SERVICE}
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["sh", "-c", "exec dotnet ${SERVICE}.dll"]
