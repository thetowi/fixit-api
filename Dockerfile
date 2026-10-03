# Etapa de build: compila el proyecto con el SDK completo de .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiamos primero solo los .csproj para aprovechar el cache de capas de Docker:
# si no cambiaron las dependencias, en el próximo build no hace falta correr "restore" de nuevo.
COPY FixIt.Domain/*.csproj FixIt.Domain/
COPY FixIt.Application/*.csproj FixIt.Application/
COPY FixIt.Infrastructure/*.csproj FixIt.Infrastructure/
COPY FixIt.Api/*.csproj FixIt.Api/
RUN dotnet restore FixIt.Api/FixIt.Api.csproj

# Ahora sí copiamos todo el código fuente y publicamos en modo Release
COPY FixIt.Domain/ FixIt.Domain/
COPY FixIt.Application/ FixIt.Application/
COPY FixIt.Infrastructure/ FixIt.Infrastructure/
COPY FixIt.Api/ FixIt.Api/
RUN dotnet publish FixIt.Api/FixIt.Api.csproj -c Release -o /app/publish --no-restore

# Etapa final: imagen liviana, solo con el runtime de ASP.NET (no el SDK completo)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# ffmpeg (03/10): FfmpegWaveformService lo usa para calcular la onda real de las notas de voz del
# chat al subirlas. Se instala acá (no hace falta en la etapa de build) para mantener la imagen
# final lo más liviana posible.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Railway asigna el puerto dinámicamente en la variable de entorno PORT.
# Se le indica a Kestrel que escuche ahí seteando ASPNETCORE_URLS en las variables
# de entorno de Railway (no acá) como: http://+:${{PORT}}
ENTRYPOINT ["dotnet", "FixIt.Api.dll"]
