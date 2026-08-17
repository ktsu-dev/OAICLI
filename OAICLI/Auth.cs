// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using Spectre.Console;

internal static class Auth
{
	/// <summary>
	/// Ensures that an API key has been provided by the user.
	/// </summary>
	internal static void EnsureHasApiKey()
	{
		AppData appData = AppData.Get();
		while (string.IsNullOrWhiteSpace(appData.ApiKey))
		{
			TextPrompt<string> textPrompt = new("Supply your OpenAI api key:");
			appData.ApiKey = AnsiConsole.Prompt(textPrompt);
			appData.Save();
		}
	}

	internal static HttpClient GetClient()
	{
		AppData appData = AppData.Get();
		HttpClient client = new();
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {appData.ApiKey}");
		return client;
	}
}
