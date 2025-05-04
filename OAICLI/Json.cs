// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.OAICLI;

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
}
