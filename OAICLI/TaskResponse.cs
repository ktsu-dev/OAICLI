namespace ktsu.OAICLI;

using System.Collections.ObjectModel;

internal class TaskResponse
{
	public string Summary { get; set; } = string.Empty;
	public Collection<FileDefinition> Files { get; set; } = [];
	public string CommitMessage { get; set; } = string.Empty;
}
