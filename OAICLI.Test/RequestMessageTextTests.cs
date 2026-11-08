// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Text.Json;

/// <summary>
/// Covers <see cref="Request.ToMessageText"/>. The payload is read by the model as text, never
/// decoded, so any escape the serializer writes reaches the model literally.
/// </summary>
[TestClass]
public sealed class RequestMessageTextTests
{
	private const string SourceLine = "List<int> xs; if (a && b) xs.Add(x => x + 1); // café — 'q'";

	/// <summary>
	/// Source and prompt characters that C# is dense with must arrive as themselves.
	/// </summary>
	[TestMethod]
	public void MessageTextKeepsSourceCharactersLiteral()
	{
		Request request = new()
		{
			Name = "Test",
			Description = "Use Assert.ThrowsExactly<T>() for exceptions.",
			Files = [new() { FilePath = "Sample.cs", Purpose = "Source", Contents = SourceLine }],
		};

		string text = request.ToMessageText();

		StringAssert.Contains(text, SourceLine);
		StringAssert.Contains(text, "Assert.ThrowsExactly<T>()");
		Assert.IsFalse(text.Contains("\\u", StringComparison.Ordinal), $"Message text contains a \\u escape: {text}");
	}

	/// <summary>
	/// Relaxed escaping must still produce JSON that round-trips to the original contents.
	/// </summary>
	[TestMethod]
	public void MessageTextIsValidJsonThatRoundTripsTheContents()
	{
		Request request = new()
		{
			Files = [new() { FilePath = "Quote.cs", Contents = "string s = \"a\\b\";\n" + SourceLine }],
		};

		string text = request.ToMessageText();

		using JsonDocument document = JsonDocument.Parse(text);
		string? contents = document.RootElement.GetProperty("files")[0].GetProperty("contents").GetString();
		Assert.AreEqual("string s = \"a\\b\";\n" + SourceLine, contents);
	}
}
