# STAGE 1: Build Frontend Assets (Node.js/Tailwind)
FROM node:20 AS frontend-build
WORKDIR /app
COPY Presentation/package.json .
COPY Presentation/package-lock.json .
RUN npm install
COPY Presentation/tailwind.config.js .
COPY Presentation/postcss.config.js .
COPY Presentation/wwwroot/css/input.css ./wwwroot/css/input.css
RUN npm run css:build # This runs the script from your package.json

# STAGE 2: Build Backend (.NET SDK)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for caching
COPY Presentation/Skyrise.sln Presentation/
COPY Presentation/Presentation.csproj Presentation/
COPY Data.Business/Data.Business.csproj Data.Business/
COPY Data.Access/Data.Access.csproj Data.Access/

# Restore packages
RUN dotnet restore "Presentation/Skyrise.sln"

# Copy the rest of the source code
COPY . .

# Copy the built CSS from the frontend stage
COPY --from=frontend-build /app/wwwroot/css/site.css ./Presentation/wwwroot/css/site.css

# Build the main project
WORKDIR "/src/Presentation"
RUN dotnet build "Presentation.csproj" -c Release -o /app/build

# STAGE 3: Publish
FROM build AS publish
RUN dotnet publish "Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

# STAGE 4: Final Production Image (ASP.NET Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Presentation.dll"]