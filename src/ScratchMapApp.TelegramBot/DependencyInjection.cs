using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScratchMapApp.TelegramBot.Infrastructure;
using ScratchMapApp.TelegramBot.Interfaces;
using ScratchMapApp.TelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace ScratchMapApp.TelegramBot;

public static class DependencyInjection
{
	public static void AddTelegramBotClient(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<ITelegramBotClient>( _ =>
		{
			var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
			if (token is null) Environment.Exit(1);

			return new TelegramBotClient(token);
		});
	}
	
	public static void AddTelegramUpdateHandler(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<IUpdateHandler>(provider =>
		{
			var botClient = provider.GetRequiredService<ITelegramBotClient>();
			var logger = provider.GetRequiredService<ILogger<UpdateHandler>>();
			var mapTileService = provider.GetRequiredService<IScratchMapService>();
			return new UpdateHandler(botClient, configuration, logger, mapTileService);
		});
	}

	public static void AddDockerClient(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<IDockerClient>(_ =>
		{
			var endpoint = configuration.GetSection("docker")["endpoint"]!;
			return new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
		});
	}

	public static void AddDockerContainerManager(this IServiceCollection services)
	{
		services.AddSingleton<IDockerContainerManager, DockerContainerManager>();
	}
	
	public static void AddTileServerHttpClient(this IServiceCollection services)
	{
		services.AddSingleton<HttpClient>();
	}

	public static void AddMapTileService(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<IScratchMapService>(provider =>
		{
			var containerManager = provider.GetRequiredService<IDockerContainerManager>();
			var httpClient = provider.GetRequiredService<HttpClient>();
			var logger = provider.GetRequiredService<ILogger<ScratchMapService>>();
			return new ScratchMapService(configuration, containerManager, httpClient, logger);
		});
	}
}