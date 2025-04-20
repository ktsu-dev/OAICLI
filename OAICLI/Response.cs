namespace ktsu.OAICLI;

using System.Collections.ObjectModel;
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
	public JsonSchema Schema { get; } = JsonSchema.FromType<Response>();
}
