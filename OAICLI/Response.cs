// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

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
	/// Gets the named schema the reply is expected to conform to.
	/// </summary>
	/// <remarks>
	/// The Chat Completions API reads the schema from <c>response_format.json_schema.schema</c> and
	/// requires a <c>name</c> beside it. A schema placed directly on <c>response_format</c> is rejected
	/// with a 400 before the model ever sees the request.
	/// </remarks>
	public JsonSchemaFormat JsonSchema { get; } = new();
}

internal class JsonSchemaFormat
{
	public string Name { get; set; } = "response";

	/// <summary>
	/// Gets the schema the reply is expected to conform to.
	/// </summary>
	/// <remarks>
	/// This is the schema's own JSON rather than the <see cref="NJsonSchema.JsonSchema"/> object,
	/// because that object is a graph: every property it holds points back at its parent. Serializing
	/// it with <see cref="System.Text.Json"/> walks that cycle and throws, which took the whole request
	/// with it. NJsonSchema knows how to write itself, so let it, and carry the result as data.
	/// </remarks>
	public JsonNode Schema { get; } = JsonNode.Parse(NJsonSchema.JsonSchema.FromType<Response>().ToJson()) ?? new JsonObject();

	/// <summary>
	/// Gets a value indicating whether the API should enforce the schema exactly.
	/// </summary>
	/// <remarks>
	/// Strict mode requires every property to be required and <c>additionalProperties</c> to be
	/// false, which the schema generated from <see cref="Response"/> does not promise.
	/// </remarks>
	public bool Strict { get; }
}
