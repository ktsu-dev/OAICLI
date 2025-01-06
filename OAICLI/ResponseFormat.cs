namespace ktsu.OAICLI;

using NJsonSchema;

internal class ResponseFormat
{
	public string Type { get; set; } = "json_schema";
	public JsonSchema Schema { get; } = JsonSchema.FromType<TaskResponse>();
}
