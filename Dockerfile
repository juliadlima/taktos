FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /projeto
COPY Directory.Build.props .
COPY Taktos.API/Taktos.API.csproj Taktos.API/
COPY Taktos.Domain/Taktos.Domain.csproj Taktos.Domain/
COPY Taktos.Infrastructure/Taktos.Infrastructure.csproj Taktos.Infrastructure/
COPY Taktos.Web/Taktos.Web.csproj Taktos.Web/
RUN dotnet restore Taktos.API/Taktos.API.csproj -p:PortalFramework=net10.0
COPY Taktos.API/ Taktos.API/
COPY Taktos.Domain/ Taktos.Domain/
COPY Taktos.Infrastructure/ Taktos.Infrastructure/
COPY Taktos.Web/ Taktos.Web/
RUN dotnet publish Taktos.API/Taktos.API.csproj -c Release -p:PortalFramework=net10.0 --no-restore -o /publicado

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publicado .
RUN install -d -o app -g app /var/taktos/chaves \
    && install -o app -g app /dev/null /var/taktos/chaves/.taktos
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Taktos.API.dll"]
