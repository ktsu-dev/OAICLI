// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Text.Json;

/// <summary>
/// Covers what the published tool depends on. Every package <c>ktsu.OAICLI</c> references is
/// restored and loaded by every install, so a package the tool never uses must not be among them.
/// </summary>
[TestClass]
public sealed class ShippedDependencyTests
{
	/// <summary>
	/// Command-line parsing is done by Spectre.Console.Cli, and Spectre.Console.Testing is for tests only.
	/// </summary>
	/// <param name="package">A package the tool must not depend on.</param>
	[TestMethod]
	[DataRow("CommandLineParser")]
	[DataRow("Spectre.Console.Testing")]
	public void ToolDoesNotDependOnAnUnusedPackage(string package)
	{
		string depsPath = Path.Combine(AppContext.BaseDirectory, "ktsu.OAICLI.deps.json");
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(depsPath));

		JsonProperty target = document.RootElement.GetProperty("targets").EnumerateObject().First();
		JsonProperty tool = target.Value.EnumerateObject().Single(library => library.Name.StartsWith("ktsu.OAICLI/", StringComparison.Ordinal));

		bool dependsOnPackage = tool.Value.TryGetProperty("dependencies", out JsonElement dependencies)
			&& dependencies.TryGetProperty(package, out _);

		Assert.IsFalse(dependsOnPackage, $"ktsu.OAICLI depends on {package}, which it never uses.");
	}
}
