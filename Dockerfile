# 1. Change SDK image to 10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./
RUN dotnet restore samples/FlexQuery.NET.Samples.WebApi/FlexQuery.NET.Samples.WebApi.csproj
RUN dotnet publish samples/FlexQuery.NET.Samples.WebApi/FlexQuery.NET.Samples.WebApi.csproj \
    -c Release -o /app/publish --no-restore

# 2. Change ASP.NET runtime image to 10.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FlexQuery.NET.Samples.WebApi.dll"]