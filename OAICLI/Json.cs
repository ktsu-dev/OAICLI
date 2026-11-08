// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Json
{
	/// <summary>
	/// Gets the JSON serializer options used for serialization.
	/// </summary>
	internal static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerOptions.Default)
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
		},
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		WriteIndented = true,
	};

	/// <summary>
	/// Gets the options used for the request payload that becomes the text of the user message.
	/// </summary>
	/// <remarks>
	/// That JSON is never decoded by the API: the model reads it as text. The default encoder would
	/// turn <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, <c>'</c>, <c>+</c> and every non-ASCII character
	/// into <c>\uXXXX</c> escapes, so the model would read obfuscated source and every request would
	/// carry the escapes as billed input. The text is never embedded in HTML, so relaxed escaping is safe.
	/// </remarks>
	internal static JsonSerializerOptions MessageTextOptions { get; } = new(SerializerOptions)
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};
}
