# 1. Use the .NET 10 SDK so it can read and restore your .NET 10 core libraries
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./
RUN dotnet restore samples/FlexQuery.NET.Samples.WebApi/FlexQuery.NET.Samples.WebApi.csproj

# 2. Keep compiling your Web API as a net8.0 application exactly as you have it
RUN dotnet publish samples/FlexQuery.NET.Samples.WebApi/FlexQuery.NET.Samples.WebApi.csproj \
    -c Release -f net8.0 -o /app/publish --no-restore

# 3. Pull the .NET 8 runtime to match your net8.0 published application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT dotnet FlexQuery.NET.Samples.WebApi.dll --urls http://0.0.0.0:${PORT:-8080}