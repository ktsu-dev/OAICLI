// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

/// <summary>
/// Reads and clears the plaintext API key that earlier versions wrote into <see cref="AppData"/>.
/// </summary>
/// <remarks>
/// This exists purely so the one-time migration in <see cref="Auth.MigrateLegacyApiKey"/> can be
/// exercised without touching the real per-user app data file.
/// </remarks>
internal interface ILegacyApiKeyStore
{
	/// <summary>
	/// Returns the stored plaintext key, or an empty string when there is none.
	/// </summary>
	public string Read();

	/// <summary>
	/// Blanks the stored plaintext key and persists the change.
	/// </summary>
	public void Clear();
}

/// <summary>
/// The real legacy store: <see cref="AppData"/>'s JSON file under the user's app data directory.
/// </summary>
internal sealed class AppDataLegacyApiKeyStore : ILegacyApiKeyStore
{
	/// <inheritdoc/>
	public string Read() => AppData.Get().ApiKey;

	/// <inheritdoc/>
	public void Clear()
	{
		AppData appData = AppData.Get();
		if (appData.ApiKey.Length == 0)
		{
			return;
		}

		appData.ApiKey = string.Empty;
		appData.Save();
	}
}
