namespace ScratchMapApp.TelegramBot.Interfaces;

public interface IDockerContainerManager
{
	public Task SendPosixSignal(string containerName, string signal);
}