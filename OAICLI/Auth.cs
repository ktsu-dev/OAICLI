// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using ktsu.CredentialCache;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;
using Spectre.Console;
using CredentialCache = ktsu.CredentialCache.CredentialCache;

/// <summary>
/// Owns the OpenAI API key: where it is kept, how it is asked for, and how it reaches a request.
/// </summary>
/// <remarks>
/// The key is a billable bearer credential, so it lives in the operating system's secret store
/// (Windows Credential Manager, macOS Keychain, libsecret on Linux) by way of
/// <see cref="CredentialCache"/> — not in the plaintext JSON file earlier versions used.
/// </remarks>
internal static class Auth
{
	/// <summary>
	/// The fixed, well-known persona under which OAICLI's single credential is stored.
	/// </summary>
	/// <remarks>
	/// <see cref="CredentialCache"/> keys every secret by <see cref="PersonaGUID"/> because it is
	/// built for the multi-persona case. OAICLI only ever holds one secret, so this is a constant
	/// rather than something generated per install — changing it would orphan every key already in
	/// the store.
	/// </remarks>
	internal const string OpenAiPersonaId = "e3b7a1d4-9c52-4f18-8a6b-0d5f2c7e41ab";

	/// <summary>
	/// Scopes OAICLI's entries within the OS secret store so they cannot collide with another ktsu
	/// tool's credentials on a shared host.
	/// </summary>
	internal const string CredentialServiceName = "ktsu.OAICLI";

	private const string NoSecretStoreMessage =
		"OAICLI keeps your OpenAI API key in the operating system's secret store, and this machine has none available. " +
		"On Linux that usually means no Secret Service provider (libsecret with GNOME Keyring or KWallet) is installed and " +
		"unlocked, which is common over SSH and in containers. Install and unlock one, then run OAICLI again. " +
		"OAICLI will not fall back to writing the key to a plain file.";

	private static readonly Lazy<CredentialCache> LazyDefaultCache =
		new(() => new CredentialCache(CreateDefaultStore()));

	/// <summary>
	/// Gets the persona built from <see cref="OpenAiPersonaId"/>.
	/// </summary>
	internal static PersonaGUID OpenAiPersona { get; } = SemanticString<PersonaGUID>.Create(OpenAiPersonaId);

	/// <summary>
	/// Gets the cache backed by this machine's secret store.
	/// </summary>
	/// <remarks>
	/// Built directly rather than taken from <see cref="CredentialCache.Instance"/> so the store
	/// carries OAICLI's own service name; the singleton can only ever use the library default.
	/// Constructed lazily so a machine with no secret store fails when a key is actually needed,
	/// rather than at class load.
	/// </remarks>
	internal static CredentialCache DefaultCache => LazyDefaultCache.Value;

	/// <summary>
	/// Ensures an API key is available, migrating one out of the legacy plaintext file if it is
	/// still there and prompting the user otherwise.
	/// </summary>
	internal static void EnsureHasApiKey() => EnsureHasApiKey(DefaultCache, new AppDataLegacyApiKeyStore());

	/// <inheritdoc cref="EnsureHasApiKey()"/>
	internal static void EnsureHasApiKey(CredentialCache cache, ILegacyApiKeyStore legacy)
	{
		MigrateLegacyApiKey(cache, legacy);

		while (!TryGetApiKey(cache, out _))
		{
			TextPrompt<string> textPrompt = new("Supply your OpenAI api key:");
			string entered = AnsiConsole.Prompt(textPrompt).Trim();
			if (entered.Length > 0)
			{
				StoreApiKey(cache, entered);
			}
		}
	}

	/// <summary>
	/// Moves a key left behind in the legacy plaintext file into the secret store, then blanks it
	/// there.
	/// </summary>
	/// <returns><see langword="true"/> when a legacy key was found, <see langword="false"/> otherwise.</returns>
	/// <remarks>
	/// The key is written to the secret store before the old copy is cleared, so a store that throws
	/// cannot lose it. Clearing matters as much as migrating: a key that merely stops being written
	/// is still a key sitting in the old file.
	/// </remarks>
	internal static bool MigrateLegacyApiKey(CredentialCache cache, ILegacyApiKeyStore legacy)
	{
		Ensure.NotNull(cache);
		Ensure.NotNull(legacy);

		string legacyKey = legacy.Read().Trim();
		if (legacyKey.Length == 0)
		{
			return false;
		}

		if (!TryGetApiKey(cache, out _))
		{
			StoreApiKey(cache, legacyKey);
		}

		legacy.Clear();
		return true;
	}

	/// <summary>
	/// Reads the stored API key.
	/// </summary>
	/// <returns><see langword="true"/> when a non-empty key was found.</returns>
	/// <exception cref="CredentialStoreException">This machine has no usable secret store.</exception>
	internal static bool TryGetApiKey(CredentialCache cache, out string apiKey)
	{
		Ensure.NotNull(cache);

		Credential? credential;
		try
		{
			if (!cache.TryGet(OpenAiPersona, out credential))
			{
				apiKey = string.Empty;
				return false;
			}
		}
		catch (Exception ex) when (IsMissingSecretStore(ex))
		{
			throw new CredentialStoreException(NoSecretStoreMessage, ex);
		}

		apiKey = credential is CredentialWithToken token ? token.Token.ToString() : string.Empty;
		return apiKey.Length > 0;
	}

	/// <summary>
	/// Writes the API key to the secret store, replacing any key already there.
	/// </summary>
	/// <exception cref="CredentialStoreException">This machine has no usable secret store.</exception>
	internal static void StoreApiKey(CredentialCache cache, string apiKey)
	{
		Ensure.NotNull(cache);
		Ensure.NotNullOrWhiteSpace(apiKey);

		CredentialWithToken credential = new() { Token = SemanticString<CredentialToken>.Create(apiKey) };

		try
		{
			cache.AddOrReplace(OpenAiPersona, credential);
		}
		catch (Exception ex) when (IsMissingSecretStore(ex))
		{
			throw new CredentialStoreException(NoSecretStoreMessage, ex);
		}
	}

	/// <summary>
	/// Builds an <see cref="HttpClient"/> carrying the stored key as a bearer token.
	/// </summary>
	internal static HttpClient GetClient() => GetClient(DefaultCache);

	/// <inheritdoc cref="GetClient()"/>
	internal static HttpClient GetClient(CredentialCache cache)
	{
		if (!TryGetApiKey(cache, out string apiKey))
		{
			throw new InvalidOperationException(
				$"No OpenAI API key is stored. Call {nameof(EnsureHasApiKey)} before building a client.");
		}

		HttpClient client = new();
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
		return client;
	}

	private static ICredentialStore CreateDefaultStore()
	{
		try
		{
			return CredentialStoreFactory.CreateDefault(CredentialServiceName);
		}
		catch (Exception ex) when (IsMissingSecretStore(ex))
		{
			throw new CredentialStoreException(NoSecretStoreMessage, ex);
		}
	}

	/// <summary>
	/// Recognises the ways a machine without a secret store makes itself known: the factory refusing
	/// the platform outright, or the native library failing to resolve on first use.
	/// </summary>
	private static bool IsMissingSecretStore(Exception exception) =>
		exception is PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException;
}
