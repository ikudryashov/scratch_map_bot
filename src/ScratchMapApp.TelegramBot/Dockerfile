FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/ScratchMapApp.TelegramBot/ScratchMapApp.TelegramBot.csproj", "src/ScratchMapApp.TelegramBot/"]
RUN dotnet restore "src/ScratchMapApp.TelegramBot/ScratchMapApp.TelegramBot.csproj"
COPY . .
WORKDIR "/src/src/ScratchMapApp.TelegramBot"
RUN dotnet build "ScratchMapApp.TelegramBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ScratchMapApp.TelegramBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ScratchMapApp.TelegramBot.dll"]
