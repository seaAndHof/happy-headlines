# One Dockerfile for all .NET services; compose passes the project name as SERVICE.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE
WORKDIR /src
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/${SERVICE}/${SERVICE}.csproj -c Release -o /app

# GAF tactic "Reduce Bundle Size": the Alpine runtime image is far smaller than the Debian one, so every image is
# smaller to store, push to the registry and pull on deploy. The services need no ICU (InvariantGlobalization).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
ARG SERVICE
ENV SERVICE=${SERVICE}
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["sh", "-c", "exec dotnet ${SERVICE}.dll"]
