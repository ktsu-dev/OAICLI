// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Net.Http.Headers;
using ktsu.CredentialCache;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;
using CredentialCache = ktsu.CredentialCache.CredentialCache;

/// <summary>
/// Covers where the OpenAI API key lives. It is a billable bearer credential, so the contract under
/// test is that it reaches the OS secret store and does not survive in the plaintext file earlier
/// versions wrote.
/// </summary>
[TestClass]
public sealed class AuthTests
{
	private static CredentialCache NewCache() => new(new InMemoryCredentialStore());

	/// <summary>
	/// Stands in for the plaintext <c>app_data.json</c> without touching the real user profile.
	/// </summary>
	private sealed class FakeLegacyApiKeyStore(string key) : ILegacyApiKeyStore
	{
		public string Key { get; private set; } = key;

		public int ClearCount { get; private set; }

		public string Read() => Key;

		public void Clear()
		{
			Key = string.Empty;
			ClearCount++;
		}
	}

	/// <summary>
	/// A store standing in for a machine whose native secret library will not load — the Linux box
	/// with no Secret Service provider, which is the common case over SSH and in containers.
	/// </summary>
	private sealed class UnavailableCredentialStore : ICredentialStore
	{
		public string Name => "Unavailable";

		public bool TryLoad(PersonaGUID persona, out Credential? credential) =>
			throw new DllNotFoundException("libsecret-1.so.0");

		public void Save(PersonaGUID persona, Credential credential) =>
			throw new DllNotFoundException("libsecret-1.so.0");

		public bool Remove(PersonaGUID persona) => throw new DllNotFoundException("libsecret-1.so.0");
	}

	/// <summary>
	/// A key written by an earlier version is moved into the secret store rather than left where it
	/// was.
	/// </summary>
	[TestMethod]
	public void MigrateMovesALegacyKeyIntoTheSecretStore()
	{
		using CredentialCache cache = NewCache();
		FakeLegacyApiKeyStore legacy = new("sk-legacy-key");

		bool migrated = Auth.MigrateLegacyApiKey(cache, legacy);

		Assert.IsTrue(migrated);
		Assert.IsTrue(Auth.TryGetApiKey(cache, out string stored));
		Assert.AreEqual("sk-legacy-key", stored);
	}

	/// <summary>
	/// Migration also blanks the old copy. A key that merely stops being written is still a key
	/// sitting in an unencrypted file, so this is the half of the migration that does the security
	/// work.
	/// </summary>
	[TestMethod]
	public void MigrateClearsThePlaintextCopy()
	{
		using CredentialCache cache = NewCache();
		FakeLegacyApiKeyStore legacy = new("sk-legacy-key");

		Auth.MigrateLegacyApiKey(cache, legacy);

		Assert.AreEqual(string.Empty, legacy.Read());
		Assert.AreEqual(1, legacy.ClearCount);
	}

	/// <summary>
	/// With nothing in the legacy file there is nothing to migrate, and no pointless write to the
	/// secret store.
	/// </summary>
	[TestMethod]
	public void MigrateDoesNothingWithoutALegacyKey()
	{
		using CredentialCache cache = NewCache();
		FakeLegacyApiKeyStore legacy = new(string.Empty);

		bool migrated = Auth.MigrateLegacyApiKey(cache, legacy);

		Assert.IsFalse(migrated);
		Assert.AreEqual(0, legacy.ClearCount);
		Assert.IsFalse(Auth.TryGetApiKey(cache, out _));
	}

	/// <summary>
	/// When both copies exist the secret store wins — a stale key in the old file must not overwrite
	/// the current one — and the stale copy is still cleared.
	/// </summary>
	[TestMethod]
	public void MigrateKeepsTheStoredKeyAndStillClearsTheStaleOne()
	{
		using CredentialCache cache = NewCache();
		Auth.StoreApiKey(cache, "sk-current-key");
		FakeLegacyApiKeyStore legacy = new("sk-stale-key");

		Auth.MigrateLegacyApiKey(cache, legacy);

		Assert.IsTrue(Auth.TryGetApiKey(cache, out string stored));
		Assert.AreEqual("sk-current-key", stored);
		Assert.AreEqual(string.Empty, legacy.Read());
	}

	/// <summary>
	/// Surrounding whitespace in a hand-pasted key is not part of the key.
	/// </summary>
	[TestMethod]
	public void MigrateTrimsTheLegacyKey()
	{
		using CredentialCache cache = NewCache();
		FakeLegacyApiKeyStore legacy = new("  sk-padded-key\n");

		Auth.MigrateLegacyApiKey(cache, legacy);

		Assert.IsTrue(Auth.TryGetApiKey(cache, out string stored));
		Assert.AreEqual("sk-padded-key", stored);
	}

	/// <summary>
	/// A legacy file holding only whitespace is not a key.
	/// </summary>
	[TestMethod]
	public void MigrateTreatsAWhitespaceLegacyKeyAsAbsent()
	{
		using CredentialCache cache = NewCache();
		FakeLegacyApiKeyStore legacy = new("   ");

		Assert.IsFalse(Auth.MigrateLegacyApiKey(cache, legacy));
		Assert.IsFalse(Auth.TryGetApiKey(cache, out _));
	}

	/// <summary>
	/// The request client takes its bearer token from the secret store.
	/// </summary>
	[TestMethod]
	public void GetClientCarriesTheStoredKeyAsABearerToken()
	{
		using CredentialCache cache = NewCache();
		Auth.StoreApiKey(cache, "sk-stored-key");

		using HttpClient client = Auth.GetClient(cache);

		AuthenticationHeaderValue? authorization = client.DefaultRequestHeaders.Authorization;
		Assert.IsNotNull(authorization);
		Assert.AreEqual("Bearer", authorization.Scheme);
		Assert.AreEqual("sk-stored-key", authorization.Parameter);
	}

	/// <summary>
	/// Building a client without a key is a programming error, not a request sent with an empty
	/// bearer token.
	/// </summary>
	[TestMethod]
	public void GetClientThrowsWhenNoKeyIsStored()
	{
		using CredentialCache cache = NewCache();

		Assert.ThrowsExactly<InvalidOperationException>(() => Auth.GetClient(cache));
	}

	/// <summary>
	/// The persona is a fixed constant. Regenerating it would orphan every key already in the store,
	/// so the value is pinned here rather than merely described in a comment.
	/// </summary>
	[TestMethod]
	public void PersonaIsTheFixedWellKnownIdentifier()
	{
		Assert.AreEqual(Auth.OpenAiPersonaId, Auth.OpenAiPersona.ToString());
		Assert.IsTrue(Guid.TryParse(Auth.OpenAiPersona.ToString(), out _));
	}

	/// <summary>
	/// A credential that is not a token is not a key, rather than an empty bearer token.
	/// </summary>
	[TestMethod]
	public void TryGetApiKeyIgnoresACredentialOfTheWrongShape()
	{
		using CredentialCache cache = NewCache();
		cache.AddOrReplace(Auth.OpenAiPersona, new CredentialWithNothing());

		Assert.IsFalse(Auth.TryGetApiKey(cache, out string stored));
		Assert.AreEqual(string.Empty, stored);
	}

	/// <summary>
	/// On a machine with no usable secret store, reading fails loudly with an explanation. The one
	/// thing it must not do is quietly fall back to a plaintext file.
	/// </summary>
	[TestMethod]
	public void ReadingWithoutASecretStoreFailsWithAnExplanation()
	{
		using CredentialCache cache = new(new UnavailableCredentialStore());

		CredentialStoreException exception =
			Assert.ThrowsExactly<CredentialStoreException>(() => Auth.TryGetApiKey(cache, out _));

		Assert.Contains("secret store", exception.Message, StringComparison.Ordinal);
		Assert.IsInstanceOfType<DllNotFoundException>(exception.InnerException);
	}

	/// <summary>
	/// The same holds for writing: no store means no key is saved anywhere.
	/// </summary>
	[TestMethod]
	public void WritingWithoutASecretStoreFailsWithAnExplanation()
	{
		using CredentialCache cache = new(new UnavailableCredentialStore());

		CredentialStoreException exception =
			Assert.ThrowsExactly<CredentialStoreException>(() => Auth.StoreApiKey(cache, "sk-key"));

		Assert.Contains("secret store", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Storing replaces whatever was there, so re-entering a key after rotating it takes effect.
	/// </summary>
	[TestMethod]
	public void StoreApiKeyReplacesAnExistingKey()
	{
		using CredentialCache cache = NewCache();
		Auth.StoreApiKey(cache, "sk-first");
		Auth.StoreApiKey(cache, "sk-second");

		Assert.IsTrue(Auth.TryGetApiKey(cache, out string stored));
		Assert.AreEqual("sk-second", stored);
	}

	/// <summary>
	/// A blank key is rejected at the point of storage rather than written and read back as nothing.
	/// </summary>
	[TestMethod]
	public void StoreApiKeyRejectsABlankKey()
	{
		using CredentialCache cache = NewCache();

		Assert.ThrowsExactly<ArgumentException>(() => Auth.StoreApiKey(cache, "   "));
	}

	/// <summary>
	/// With a key already in the store there is nothing to prompt for, so this completes without a
	/// console at all — which is what lets the migration path run unattended.
	/// </summary>
	[TestMethod]
	public void EnsureHasApiKeyIsSatisfiedByTheMigratedKey()
	{
		using CredentialCache cache = NewCache();
		FakeLegacyApiKeyStore legacy = new("sk-legacy-key");

		Auth.EnsureHasApiKey(cache, legacy);

		Assert.IsTrue(Auth.TryGetApiKey(cache, out string stored));
		Assert.AreEqual("sk-legacy-key", stored);
		Assert.AreEqual(string.Empty, legacy.Read());
	}

	/// <summary>
	/// The persona is built through the same semantic-string factory the cache keys on, so a token
	/// stored under it round-trips.
	/// </summary>
	[TestMethod]
	public void StoredCredentialIsATokenUnderTheOpenAiPersona()
	{
		using CredentialCache cache = NewCache();
		Auth.StoreApiKey(cache, "sk-stored-key");

		Assert.IsTrue(cache.TryGet(SemanticString<PersonaGUID>.Create(Auth.OpenAiPersonaId), out Credential? credential));
		Assert.IsInstanceOfType<CredentialWithToken>(credential);
		Assert.AreEqual("sk-stored-key", ((CredentialWithToken)credential!).Token.ToString());
	}
}
