using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScratchMapApp.TelegramBot.Exceptions;
using ScratchMapApp.TelegramBot.Interfaces;
using ScratchMapApp.TelegramBot.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace ScratchMapApp.TelegramBot.Services;

public partial class UpdateHandler : IUpdateHandler
{
	private readonly ITelegramBotClient _botClient;
	private readonly ILogger<UpdateHandler> _logger;
	private readonly IScratchMapService _scratchMapService;
	private readonly List<Country> _supportedCountries;
	private static readonly SemaphoreSlim RequestSemaphore = new (1);
	private readonly Dictionary<string, string> _botMessages;

	public UpdateHandler(ITelegramBotClient botClient, IConfiguration configuration, ILogger<UpdateHandler> logger, IScratchMapService scratchMapService)
	{
		_botClient = botClient;
		_logger = logger;
		_scratchMapService = scratchMapService;

		// Populate supported countries list and bot messages dictionary from corresponding JSON files
		var supportedCountriesPath = configuration.GetValue<string>("supportedCountriesJsonPath");
		var botMessagesPath = configuration.GetValue<string>("botMessagesJsonPath");

		if (supportedCountriesPath is null || botMessagesPath is null)
		{
			LogErrorAndExit("Configuration file path(s) not specified");
		}
		
		var supportedCountriesJson = File.ReadAllText(supportedCountriesPath!);
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};
		var supportedCountries = JsonSerializer.Deserialize<List<Country>>(supportedCountriesJson, options);
		
		var botMessagesJson = File.ReadAllText(botMessagesPath!);
		var botMessages = JsonSerializer.Deserialize<Dictionary<string, string>>(botMessagesJson);

		if (supportedCountries is null || botMessages is null)
		{
			LogErrorAndExit("Configuration file(s) not found");
		}
		
		_supportedCountries = supportedCountries!;
		_botMessages = botMessages!;
	}

	public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
	{
		// Process only text messages: https://core.telegram.org/bots/api#message
		if (update.Message is not { } message || message.Text is null) return;

		await HandleMessage(message);
	}
	
	public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
	{
		_logger.LogError("Polling error {1} occured at {2:h:mm:ss tt zz}", exception.Message, DateTime.UtcNow);
		await Console.Error.WriteLineAsync(exception.ToString());
	}
	
	private async Task HandleMessage(Message message)
	{
		var user = message.From;
		var text = message.Text;
    
		if (user is null) return;
		
		_logger.LogInformation("Message received: {1}", text);

		if (text!.StartsWith("/"))
		{
			await HandleCommand(user, text);
		}
		else
		{
			await SelectCountries(user, text);
		}
	}

	private async Task HandleCommand(User user, string text)
	{
		switch (text)
		{
			case "/start":
				await _botClient.SendTextMessageAsync(user.Id, _botMessages["WelcomeMessage"], parseMode: ParseMode.MarkdownV2);
				break;
			case "/info":
				await _botClient.SendTextMessageAsync(user.Id, _botMessages["InfoMessage"], parseMode: ParseMode.MarkdownV2);
				break;
			default:
				await _botClient.SendTextMessageAsync(user.Id, _botMessages["UnknownCommandMessage"], parseMode: ParseMode.MarkdownV2);
				break;
		}
	}
	
	private async Task SelectCountries(User user, string text)
	{
		text = text.Trim();

		if (!CountryListRegex().IsMatch(text))
		{
			await _botClient.SendTextMessageAsync(user.Id, _botMessages["InvalidInputMessage"], parseMode: ParseMode.MarkdownV2);
			return;
		}
		
		var inputCountries = text.Split(", ");
		var selectedCountries = new HashSet<Country>();
		var unrecognizedInputs = new HashSet<string>();
		var response = _botMessages["SelectedCountriesListHeader"];

		foreach (var country in inputCountries)
		{
			if (string.IsNullOrWhiteSpace(country)) continue;
			
			// Check provided country name against supported countries and their aliases, ignoring the case
			var supportedCountry = _supportedCountries.SingleOrDefault(c => 
				string.Compare(c.Name, country, StringComparison.OrdinalIgnoreCase) == 0
			    || c.Aliases.Any(a => string.Compare(a, country, StringComparison.OrdinalIgnoreCase) == 0));

			if (supportedCountry is null)
			{
				unrecognizedInputs.Add(country);
				continue;
			}
			
			var added = selectedCountries.Add(supportedCountry);
			if (added) response += $"\n{supportedCountry.Emoji}{supportedCountry.Name}";
		}

		if (selectedCountries.Count == 0)
		{
			await _botClient.SendTextMessageAsync(user.Id, _botMessages["NoValidInputsMessage"], parseMode: ParseMode.MarkdownV2);
			return;
		}

		if (unrecognizedInputs.Count != 0)
		{
			response += _botMessages["UnrecognizedInputsListHeader"];
			foreach (var input in unrecognizedInputs)
			{
				response += $"\n\\- {input}";
			}
		}
		
		// Return response
		await _botClient.SendTextMessageAsync(user.Id, response, parseMode: ParseMode.MarkdownV2);
		await _botClient.SendTextMessageAsync(user.Id, _botMessages["ScratchmapGenerationMessage"], parseMode: ParseMode.MarkdownV2);
		
		// Create scratch map
		// Semaphore is used to prevent concurrency issues, as all requests will result in reading and
		// modifying the same tileserver gl JSON configuration file
		await RequestSemaphore.WaitAsync();
        
		try
		{
			var scratchMapPath = await _scratchMapService.GetScratchMap(selectedCountries);
			await using var stream = File.OpenRead(scratchMapPath);
			await _botClient.SendDocumentAsync(
				user.Id, 
				InputFile.FromStream(stream, Path.GetFileName(scratchMapPath)),
				caption: _botMessages["ScratchmapCreatedMessage"],
				parseMode: ParseMode.MarkdownV2);
			File.Delete(scratchMapPath);
		}
		catch (Exception ex)
		{
			switch (ex)
			{
				case ConfigurationException:
					LogErrorAndExit(ex.Message);
					break;
				default:
					_scratchMapService.ResetMapStyle();
					LogErrorAndExit($"{ex.Message}");
					break;
			}
		}
        
        RequestSemaphore.Release();
	}

	private void LogErrorAndExit(string error)
	{
		_logger.LogCritical("{1} error occured at {2:h:mm:ss tt zz}, stopping application.", 
			error, DateTime.UtcNow);
		Environment.Exit(1);
	}

	// Compiled regex to check if the list of countries is in the correct format
	// Matches latin alphabet of any case and spaces, words should be separated by ", "
    [GeneratedRegex("^(?:[A-Za-z ]+(?:, )?)+$")]
    private static partial Regex CountryListRegex();
}