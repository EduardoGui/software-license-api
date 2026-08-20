FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SoftwareLicense.Api.csproj ./
RUN dotnet restore SoftwareLicense.Api.csproj

COPY . .
RUN dotnet publish SoftwareLicense.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SoftwareLicense.Api.dll"]
