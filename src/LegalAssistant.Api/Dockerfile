FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# copy whole repository into the container build context
COPY . ./
# publish the API project from the repository root
RUN dotnet publish "src/LegalAssistant.Api/LegalAssistant.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LegalAssistant.Api.dll"]
