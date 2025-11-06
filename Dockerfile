FROM node:20 AS frontend-build
WORKDIR /app


COPY Presentation/ ./Presentation/


WORKDIR /app/Presentation


RUN npm install


RUN npm run css:build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Presentation/Skyrise.sln Presentation/
COPY Presentation/Presentation.csproj Presentation/
COPY Data.Business/Data.Business.csproj Data.Business/
COPY Data.Access/Data.Access.csproj Data.Access/


RUN dotnet restore "Presentation/Skyrise.sln"


COPY . .


COPY --from=frontend-build /app/Presentation/wwwroot/css/site.css ./Presentation/wwwroot/css/site.css



WORKDIR "/src/Presentation"
RUN dotnet build "Presentation.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish "Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Presentation.dll"]