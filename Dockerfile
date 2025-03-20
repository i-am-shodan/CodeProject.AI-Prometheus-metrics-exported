FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

WORKDIR /app

COPY ./ ./

RUN dotnet publish -c Release -o out aspnetcoreapp.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine
COPY --from=build-env /app/out .

# Expose ports
EXPOSE 5096/tcp

# Start
ENTRYPOINT ["dotnet", "aspnetcoreapp.dll"]