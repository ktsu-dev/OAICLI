// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using NJsonSchema;

internal class Response
{
	public string Summary { get; set; } = string.Empty;
	public Collection<FileDefinition> Files { get; set; } = [];
	public string CommitMessage { get; set; } = string.Empty;
}

internal class ResponseFormat
{
	public string Type { get; set; } = "json_schema";

	/// <summary>
	/// Gets the schema the reply is expected to conform to.
	/// </summary>
	/// <remarks>
	/// This is the schema's own JSON rather than the <see cref="JsonSchema"/> object, because that
	/// object is a graph: every property it holds points back at its parent. Serializing it with
	/// <see cref="System.Text.Json"/> walks that cycle and throws, which took the whole request with
	/// it. NJsonSchema knows how to write itself, so let it, and carry the result as data.
	/// </remarks>
	public JsonNode Schema { get; } = JsonNode.Parse(JsonSchema.FromType<Response>().ToJson()) ?? new JsonObject();
}
