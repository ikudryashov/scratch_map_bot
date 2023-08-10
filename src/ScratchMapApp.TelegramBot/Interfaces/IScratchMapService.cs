using ScratchMapApp.TelegramBot.Models;

namespace ScratchMapApp.TelegramBot.Interfaces;

public interface IScratchMapService
{
	public Task<string> GetScratchMap(HashSet<Country> countries);
	public void ResetMapStyle();
}