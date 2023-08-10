using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScratchMapApp.TelegramBot;
using ScratchMapApp.TelegramBot.Interfaces;
using ScratchMapApp.TelegramBot.Services;
using Serilog;

var host = Host.CreateDefaultBuilder()
	.ConfigureHostConfiguration(config =>
	{
		config.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Configuration"))
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.AddJsonFile(
				$"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
				optional: true)
			.AddEnvironmentVariables();
	})
	.UseSerilog((context, serilogConfiguration) =>
	{
		serilogConfiguration.ReadFrom.Configuration(context.Configuration);
	})
	.ConfigureServices((context, services) =>
	{
		var configuration = context.Configuration;
		
		services.AddSingleton(configuration);
		services.AddTelegramBotClient(configuration);
		services.AddDockerClient(configuration);
		services.AddDockerContainerManager();
		services.AddTelegramUpdateHandler(configuration);
		services.AddTileServerHttpClient();
		services.AddMapTileService(configuration);
		services.AddSingleton<ITelegramBotService, TelegramBotService>();
	})
	.Build();

var botService = host.Services.GetRequiredService<ITelegramBotService>();

await botService.Run();