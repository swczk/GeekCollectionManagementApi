FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
ENV \
	ASPNETCORE_HTTP_PORTS=8080 \
	DOTNET_RUNNING_IN_CONTAINER=true \
	DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Web/Web.csproj", "Web/"]
RUN dotnet restore "Web/Web.csproj"
COPY . .
WORKDIR "/src/Web"
RUN dotnet build "Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [ "dotnet", "Web.dll" ]
