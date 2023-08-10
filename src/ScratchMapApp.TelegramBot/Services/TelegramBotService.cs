using Microsoft.Extensions.Logging;
using ScratchMapApp.TelegramBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace  ScratchMapApp.TelegramBot.Services;

public class TelegramBotService : ITelegramBotService
{
	private readonly ILogger<ITelegramBotService> _logger;
	private readonly ITelegramBotClient _client;
	private readonly IUpdateHandler _updateHandler;

	public TelegramBotService(
		ITelegramBotClient client,
		IUpdateHandler updateHandler,
		ILogger<ITelegramBotService> logger)
	{
		_client = client;
		_updateHandler = updateHandler;
		_logger = logger;
	}
	
	public async Task Run()
	{
		using CancellationTokenSource cts = new ();
		
		_client.StartReceiving(
			updateHandler: _updateHandler.HandleUpdateAsync,
			pollingErrorHandler: _updateHandler.HandlePollingErrorAsync,
			cancellationToken: cts.Token
		);

		_logger.LogInformation("Started receiving requests.");

		// Keep the application running until the cancellation token is triggered
		await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);

		_logger.LogInformation("Application stopped.");
	}
}