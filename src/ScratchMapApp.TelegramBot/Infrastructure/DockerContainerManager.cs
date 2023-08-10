using Docker.DotNet;
using Docker.DotNet.Models;
using ScratchMapApp.TelegramBot.Interfaces;

namespace ScratchMapApp.TelegramBot.Infrastructure;

public class DockerContainerManager : IDockerContainerManager
{
	private readonly IDockerClient _client;

	public DockerContainerManager(IDockerClient client)
	{
		_client = client;
	}
	
	public async Task SendPosixSignal (string containerName, string signal)
	{
		var container = await _client.Containers.InspectContainerAsync(containerName);
		if (container != null)
		{
			var containerId = container.ID;
			await _client.Containers.KillContainerAsync(containerId, new ContainerKillParameters { Signal = signal });
		}
		else
		{
			Console.WriteLine($"Container {containerName} not found.");
		}
	}
}