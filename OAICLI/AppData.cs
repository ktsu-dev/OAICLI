// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using ktsu.AppDataStorage;

internal class AppData : AppData<AppData>
{
	/// <summary>
	/// The OpenAI API key as earlier versions stored it: plaintext, in this object's JSON file.
	/// </summary>
	/// <remarks>
	/// Retained only so <see cref="Auth.MigrateLegacyApiKey"/> can move an existing key into the OS
	/// credential store and then blank this field. Nothing writes a key here any more — read the key
	/// through <see cref="Auth.TryGetApiKey"/> instead.
	/// </remarks>
	public string ApiKey { get; set; } = string.Empty;
}
