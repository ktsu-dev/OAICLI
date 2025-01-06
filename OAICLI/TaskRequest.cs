namespace ktsu.OAICLI;

using System.Collections.ObjectModel;

internal class TaskRequest
{
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Collection<FileDefinition> Files { get; set; } = [];
}
