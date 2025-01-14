﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace QuickEdit.Commands;
public class InteractionServiceHandler
{
	private static readonly DiscordSocketClient? _client = Program.client;
	private static InteractionService? _interactionService;
	private static readonly InteractionServiceConfig _interactionServiceConfig = new() { UseCompiledLambda = true, DefaultRunMode = RunMode.Async };

	/// <summary>
	/// Initialize the InteractionService
	/// </summary>
	public static async Task InitAsync()
	{
		try
		{
			_interactionService = new InteractionService(_client!.Rest, _interactionServiceConfig);
			await RegisterModulesAsync();

			// Can't simply get the result of the ExecuteCommandAsync, because of RunMode.Async
			// So the event has to be used to handle the result
			_interactionService.SlashCommandExecuted += OnSlashCommandExecutedAsync;
		}
		catch
		{
			await Program.LogAsync("InteractionServiceHandler", "Error initializing InteractionService", LogSeverity.Critical);
			throw;
		}
	}

	/// <summary>
	/// Register modules / commands
	/// </summary>
	public static async Task RegisterModulesAsync()
	{
		// The service might not have been initialized yet
		if (_interactionService == null)
		{
			await Program.LogAsync("InteractionServiceManager.RegisterModulesAsync()", "InteractionService not initialized yet", LogSeverity.Error);
			throw new InvalidOperationException("InteractionService not initialized while trying to register commands");
		}

		try
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies)
			{
				await _interactionService.AddModulesAsync(assembly, null);
			}

			await _interactionService.RegisterCommandsGloballyAsync();
			_client!.InteractionCreated += OnInteractionCreatedAsync;
			await Program.LogAsync("InteractionServiceManager", "Modules registered successfully", LogSeverity.Info);
		}
		catch (Exception e)
		{
			await Program.LogAsync("InteractionServiceManager", $"Error registering modules. ({e})", LogSeverity.Critical);
			throw;
		}
	}

	public static async Task OnInteractionCreatedAsync(SocketInteraction interaction)
	{
		// The service might not have been initialized yet
		if (_interactionService == null)
		{
			await Program.LogAsync("InteractionServiceManager.OnInteractionCreatedAsync()", "InteractionService not initialized yet", LogSeverity.Error);
			return;
		}

		try
		{
			var ctx = new SocketInteractionContext(_client, interaction);
			await _interactionService.ExecuteCommandAsync(ctx, null);
			// Result is handled in OnSlashCommandExecutedAsync, since the RunMode is RunMode.Async.
			// See https://docs.discordnet.dev/guides/int_framework/post-execution.html for more info.
		}
		catch (Exception e)
		{
			await Program.LogAsync("InteractionServiceManager", $"Error handling interaction. {e.Message}", LogSeverity.Error);

			if (interaction.Type is InteractionType.ApplicationCommand)
			{
				await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
			}

			throw;
		}
	}

	public static async Task OnSlashCommandExecutedAsync(SlashCommandInfo commandInfo, IInteractionContext interactionContext, IResult result) {
		// Only trying to handle errors lol
		if (result.IsSuccess)
			return;

		try
		{
			await Program.LogAsync("InteractionServiceManager", $"Error handling interaction: {result.Error}", LogSeverity.Error);
			await interactionContext.Interaction.FollowupAsync("An error occurred while executing the command.", ephemeral: true);
		}
		catch (Exception e)
		{
			await Program.LogAsync("InteractionServiceManager", $"Error handling interaction exception bruh: {e.ToString()}", LogSeverity.Error);
			throw;
		}
	}
}
