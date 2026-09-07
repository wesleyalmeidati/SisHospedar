# PT: Build multi-stage — o SDK completo só existe na imagem intermediária; a
#     imagem final leva apenas o runtime do ASP.NET Core, bem menor. Restaura só
#     com o .csproj antes de copiar o resto do código, pra cachear a camada de
#     pacotes NuGet entre builds quando só o código-fonte muda.
# EN: Multi-stage build — the full SDK only exists in the intermediate image; the
#     final image ships just the ASP.NET Core runtime, much smaller. Restores
#     with only the .csproj before copying the rest of the source, to cache the
#     NuGet package layer across builds when only source code changes.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SisHospedar.csproj .
RUN dotnet restore SisHospedar.csproj
COPY . .
RUN dotnet publish SisHospedar.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SisHospedar.dll"]
