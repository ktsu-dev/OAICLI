// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.OAICLI;
using Spectre.Console;

internal static class Auth
{
	/// <summary>
	/// Ensures that an API key has been provided by the user.
	/// </summary>
	internal static void EnsureHasApiKey()
	{
		var appData = AppData.Get();
		while (string.IsNullOrWhiteSpace(appData.ApiKey))
		{
			TextPrompt<string> textPrompt = new("Supply your OpenAI api key:");
			appData.ApiKey = AnsiConsole.Prompt(textPrompt);
			appData.Save();
		}
	}

	internal static HttpClient GetClient()
	{
		var appData = AppData.Get();
		HttpClient client = new();
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {appData.ApiKey}");
		return client;
	}
}
