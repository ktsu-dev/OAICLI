// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ktsu.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;

internal abstract class CodeReviewCommand : Command<CodeReviewCommand.Settings>
{
	internal abstract Request TaskRequest { get; }

	internal virtual void Setup(Settings settings) { }

	public sealed class Settings : CommandSettings
	{
		[CommandArgument(0, $"[{nameof(FilePath)}]")]
		public string FilePath { get; internal set; } = string.Empty;

		[CommandOption("-c|--context <FILE_PATHS>")]
		public string[] ContextFilePaths { get; internal set; } = [];

		[CommandOption("-f|--force")]
		public bool Force { get; init; }
	}

	/// <summary>
	/// The exit code reported when the API request did not succeed, so a script gating on this tool
	/// can tell a failed review from a completed one.
	/// </summary>
	internal const int RequestFailedExitCode = 1;

	protected override int Execute([NotNull] CommandContext context, [NotNull] Settings settings, CancellationToken cancellationToken) =>
		Run(TaskRequest.Send, () => Setup(settings));

	//string responseJson = OAICLI.MakeRequest(TaskRequest);
	//var jsonNode = JsonNode.Parse(responseJson);
	//var responseObj = jsonNode as JsonObject;
	//var choicesArray = responseObj?["choices"] as JsonArray;
	//var choiceObj = choicesArray?[0] as JsonObject;
	//var messageObj = choiceObj?["message"] as JsonObject;
	//string contentModified = messageObj?["content"]?.ToString() ?? "";

	//if (!string.IsNullOrWhiteSpace(contentModified))
	//{
	//	string fileDir = Path.GetDirectoryName(settings.FilePath) ?? string.Empty;
	//	if (string.IsNullOrWhiteSpace(fileDir))
	//	{
	//		throw new InvalidOperationException($"Invalid file path: {settings.FilePath}");
	//	}

	//	if (!Directory.Exists(fileDir))
	//	{
	//		throw new DirectoryNotFoundException($"Directory not found: {fileDir}");
	//	}

	//	string tmpFilePath = Path.Combine(fileDir, Path.GetFileNameWithoutExtension(settings.FilePath) + ".tmp" + Path.GetExtension(settings.FilePath));
	//	File.WriteAllText(tmpFilePath, contentModified);

	//	if (!string.IsNullOrWhiteSpace(aiResponse))
	//	{
	//		Console.WriteLine(aiResponse);
	//	}

	//	if (!settings.Force)
	//	{
	//		_ = Process.Start(new ProcessStartInfo()
	//		{
	//			FileName = "code",
	//			Arguments = $"--diff {settings.FilePath} {tmpFilePath}",
	//			UseShellExecute = true,
	//		});
	//	}

	//	bool takeChange = settings.Force;
	//	if (!takeChange)
	//	{
	//		var textPrompt = new ConfirmationPrompt("Take the change?");
	//		takeChange = AnsiConsole.Prompt(textPrompt);
	//		Console.WriteLine(takeChange ? "Confirmed" : "Declined");
	//	}

	//	if (takeChange)
	//	{
	//		File.Delete(settings.FilePath);
	//		File.Move(tmpFilePath, settings.FilePath);
	//	}
	//	else
	//	{
	//		File.Delete(tmpFilePath);
	//	}
	//}

	/// <summary>
	/// Sends the request, and changes the user's files only once it has succeeded.
	/// </summary>
	/// <remarks>
	/// A run that never got an answer -- no key, a rejection, a timeout -- has nothing to apply, so it
	/// must leave the solution as it found it. Scaffolding ahead of the request left a project behind
	/// on every failed run, and the user's build broke until they removed it by hand.
	/// </remarks>
	/// <param name="sendRequest">Sends the request and returns the body of the response.</param>
	/// <param name="applyResult">Writes the command's changes, called only after a successful request.</param>
	/// <returns>The exit code from <see cref="SendRequest"/>.</returns>
	internal static int Run([NotNull] Func<string> sendRequest, [NotNull] Action applyResult)
	{
		Ensure.NotNull(applyResult);

		int exitCode = SendRequest(sendRequest);
		if (exitCode == 0)
		{
			applyResult();
		}

		return exitCode;
	}

	/// <summary>
	/// Runs the request and turns its outcome into an exit code.
	/// </summary>
	/// <remarks>
	/// Returning zero regardless of what came back makes a rejected request — an expired key, a rate
	/// limit, an unreachable endpoint — look to the caller exactly like a completed one, so the
	/// failure has to reach the exit code and the console rather than being printed as a result.
	/// </remarks>
	/// <param name="sendRequest">Sends the request and returns the body of the response.</param>
	/// <returns>Zero when the request succeeded, <see cref="RequestFailedExitCode"/> when it did not.</returns>
	internal static int SendRequest([NotNull] Func<string> sendRequest)
	{
		Ensure.NotNull(sendRequest);

		try
		{
			_ = sendRequest();
			return 0;
		}
		catch (HttpRequestException ex)
		{
			return ReportFailure(Describe(ex));
		}
		catch (AggregateException ex) when (ex.InnerException is HttpRequestException or TaskCanceledException)
		{
			return ReportFailure(Describe(ex.InnerException));
		}
		catch (TaskCanceledException ex)
		{
			return ReportFailure(Describe(ex));
		}
	}

	/// <summary>
	/// Puts a failure into the terms the person running the command needs.
	/// </summary>
	/// <remarks>
	/// A request the API rejected already carries its status and error body in the message. One that
	/// never reached the API has no status, and saying so is the difference between "your key is
	/// wrong" and "your network is down".
	/// </remarks>
	/// <param name="ex">The failure to describe, which <see cref="SendRequest"/> has already narrowed
	/// to a rejection, a send that did not happen, or a timeout.</param>
	/// <returns>The message to report.</returns>
	private static string Describe(Exception ex) => ex switch
	{
		HttpRequestException { StatusCode: not null } rejected => rejected.Message,
		HttpRequestException unsent => $"The OpenAI API request could not be sent: {unsent.Message}",

		// Everything else SendRequest catches is a timeout; it does not reach here by any other route.
		_ => $"The OpenAI API request timed out: {ex.Message}",
	};

	/// <summary>
	/// Writes the failure to the console and hands back the exit code that goes with it.
	/// </summary>
	/// <param name="message">The failure to report.</param>
	/// <returns><see cref="RequestFailedExitCode"/>.</returns>
	private static int ReportFailure(string message)
	{
		AnsiConsole.MarkupLineInterpolated($"[red]{message}[/]");
		return RequestFailedExitCode;
	}

	internal static string EnsureTrainingNewLine(string contentOriginal, string contentModified)
	{
		LineEndingStyle lineEndingsOriginal = contentOriginal.DetermineLineEndings();
		contentModified = contentModified.NormalizeLineEndings(LineEndingStyle.Unix);
		contentModified = contentModified.Trim();
		contentModified += "\n";

		contentModified = contentModified.NormalizeLineEndings(lineEndingsOriginal);
		return contentModified;
	}

	/// <summary>
	/// Adds one project entry to a solution's project list, immediately before the first global
	/// section.
	/// </summary>
	/// <remarks>
	/// Anchored on the <c>Global</c> line rather than on an <c>EndProject</c> one. Replacing
	/// "EndProject" rewrote every occurrence of it: each existing project gained a copy of the new
	/// entry, all sharing one GUID, and lost its own terminator to the replacement -- so no solution
	/// with more than one project survived. "EndProject" is also a prefix of "EndProjectSection", so
	/// a solution carrying solution folders or nested-project sections had those mangled too.
	/// <para>
	/// The entry is passed in with Unix newlines and converted to whatever the solution already uses,
	/// so editing a file does not leave it with two conventions in it.
	/// </para>
	/// </remarks>
	/// <param name="solutionContent">The solution file's current content.</param>
	/// <param name="projectEntry">
	/// The <c>Project(...)</c> through <c>EndProject</c> block to add, with Unix newlines and no
	/// trailing one.
	/// </param>
	/// <returns>The solution content with the entry added once.</returns>
	internal static string InsertProjectEntry(string solutionContent, string projectEntry)
	{
		LineEndingStyle lineEndings = solutionContent.DetermineLineEndings();
		string block = $"{projectEntry}\n".NormalizeLineEndings(lineEndings);

		// The project list ends where the first global section begins. GlobalSection lines are nested
		// inside that section, so the first line starting with "Global" is always the section header.
		int globalIndex = solutionContent.IndexOf("\nGlobal", StringComparison.Ordinal);
		if (globalIndex < 0)
		{
			// No global section to sit in front of, so the entry goes last, on a line of its own.
			string separator = solutionContent.EndsWith('\n') ? string.Empty : "\n".NormalizeLineEndings(lineEndings);
			return string.Concat(solutionContent, separator, block);
		}

		return solutionContent.Insert(globalIndex + 1, block);
	}

	/// <summary>
	/// Maps a project onto every configuration the solution declares.
	/// </summary>
	/// <remarks>
	/// A project with no <c>ProjectConfigurationPlatforms</c> lines is listed by the solution but
	/// never built by it, so <c>dotnet build</c> and <c>dotnet test</c> on the solution skip it
	/// without a word. Each solution configuration is mapped to the same configuration on
	/// <c>Any CPU</c>, which is what <c>dotnet sln add</c> writes for an SDK-style project.
	/// </remarks>
	/// <param name="solutionContent">The solution file's current content.</param>
	/// <param name="projectGuid">The project's GUID, braces included.</param>
	/// <returns>
	/// The solution content with the project's configuration lines added, or unchanged when the
	/// solution declares no configurations.
	/// </returns>
	internal static string AddProjectConfigurations(string solutionContent, string projectGuid)
	{
		const string solutionConfigurationsHeader = "GlobalSection(SolutionConfigurationPlatforms) = preSolution";
		const string projectConfigurationsHeader = "GlobalSection(ProjectConfigurationPlatforms) = postSolution";
		const string sectionEnd = "EndGlobalSection";

		LineEndingStyle lineEndings = solutionContent.DetermineLineEndings();
		List<string> lines = [.. solutionContent.Split('\n').Select(line => line.TrimEnd('\r'))];

		int solutionConfigurationsStart = lines.FindIndex(line => line.Trim() == solutionConfigurationsHeader);
		if (solutionConfigurationsStart < 0)
		{
			return solutionContent;
		}

		int solutionConfigurationsEnd = lines.FindIndex(solutionConfigurationsStart, line => line.Trim() == sectionEnd);
		if (solutionConfigurationsEnd < 0)
		{
			return solutionContent;
		}

		List<string> configurations = [.. lines
			.Skip(solutionConfigurationsStart + 1)
			.Take(solutionConfigurationsEnd - solutionConfigurationsStart - 1)
			.Select(line => line.Split('=')[0].Trim())
			.Where(configuration => configuration.Length > 0)];
		if (configurations.Count == 0)
		{
			return solutionContent;
		}

		List<string> mappings = [];
		foreach (string configuration in configurations)
		{
			string projectConfiguration = $"{configuration.Split('|')[0]}|Any CPU";
			mappings.Add($"\t\t{projectGuid}.{configuration}.ActiveCfg = {projectConfiguration}");
			mappings.Add($"\t\t{projectGuid}.{configuration}.Build.0 = {projectConfiguration}");
		}

		int projectConfigurationsStart = lines.FindIndex(line => line.Trim() == projectConfigurationsHeader);
		if (projectConfigurationsStart < 0)
		{
			// No project has been mapped yet, so the section itself goes in after the solution's own.
			lines.InsertRange(solutionConfigurationsEnd + 1, [$"\t{projectConfigurationsHeader}", .. mappings, $"\t{sectionEnd}"]);
		}
		else
		{
			int projectConfigurationsEnd = lines.FindIndex(projectConfigurationsStart, line => line.Trim() == sectionEnd);
			lines.InsertRange(projectConfigurationsEnd, mappings);
		}

		return string.Join("\n", lines).NormalizeLineEndings(lineEndings);
	}

	internal static string FindSolutionAbove(string path) =>
	FindFileAbove(path, "*.sln");

	internal static string FindCSProjAbove(string path) =>
			FindFileAbove(path, "*.csproj");

	internal static string[] FindCSCodeBelow(string path) =>
			FindFilesBelow(path, "*.cs");

	internal static string FindFileAbove(string path, string pattern)
	{
		bool isFile = File.Exists(path);
		string currentDir = isFile
			? Path.GetDirectoryName(path) ?? string.Empty
			: path;

		while (!string.IsNullOrWhiteSpace(currentDir))
		{
			if (!Directory.Exists(currentDir))
			{
				// A guessed starting directory may not exist; its nearest existing ancestor is where
				// the walk really begins.
				currentDir = Path.GetDirectoryName(currentDir) ?? string.Empty;
				continue;
			}

			string[] directoryFiles = Directory.GetFiles(currentDir, pattern);
			if (directoryFiles.Length > 0)
			{
				return Path.GetFullPath(directoryFiles[0]);
			}

			currentDir = Path.GetDirectoryName(currentDir) ?? string.Empty;
		}

		return string.Empty;
	}

	internal static string[] FindFilesBelow(string path, string pattern)
	{
		bool isFile = File.Exists(path);
		string currentDir = isFile
			? Path.GetDirectoryName(path) ?? string.Empty
			: path;

		return string.IsNullOrWhiteSpace(currentDir)
			? []
			: [.. Directory.GetFiles(currentDir, pattern, SearchOption.AllDirectories)
				.Where(filePath => !IsBuildOutputOrGenerated(Path.GetRelativePath(currentDir, filePath)))];
	}

	/// <summary>
	/// Directories whose contents the build or source control writes, not the user.
	/// </summary>
	private static readonly string[] ExcludedDirectoryNames = ["bin", "obj", ".git"];

	/// <summary>
	/// File name endings the build and designers use for machine-generated sources.
	/// </summary>
	private static readonly string[] GeneratedFileSuffixes = [".g.cs", ".g.i.cs", ".designer.cs"];

	/// <summary>
	/// Whether a file found below the search root is build output or generated code, which would only
	/// cost tokens and crowd out the user's own code if it were sent to the model.
	/// </summary>
	/// <param name="relativePath">The file's path relative to the search root, so that directories
	/// above the root never count.</param>
	/// <returns><see langword="true"/> when the file should be left out.</returns>
	internal static bool IsBuildOutputOrGenerated(string relativePath)
	{
		string[] segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0)
		{
			return false;
		}

		bool underExcludedDirectory = segments[..^1].Any(segment =>
			ExcludedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
		string fileName = segments[^1];
		bool generated = GeneratedFileSuffixes.Any(suffix =>
			fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

		return underExcludedDirectory || generated;
	}

	internal static bool FindProjectAndSolutionFilePaths(string path, out string solutionFilePath, out string projectFilePath)
	{
		solutionFilePath = FindSolutionAbove(path);
		projectFilePath = FindCSProjAbove(path);

		bool foundSolution = !string.IsNullOrEmpty(solutionFilePath);
		bool foundProject = !string.IsNullOrEmpty(projectFilePath);
		if (foundSolution && !foundProject)
		{
			string solutionDir = Path.GetDirectoryName(solutionFilePath) ?? string.Empty;
			string solutionName = Path.GetFileNameWithoutExtension(solutionFilePath);
			string projectDir = Path.Join(solutionDir, solutionName);
			projectFilePath = Directory.Exists(projectDir)
				? FindCSProjAbove(projectDir)
				: FindProjectNamedAfterSolutionBelow(solutionDir, solutionName);
		}

		return !string.IsNullOrEmpty(solutionFilePath) && !string.IsNullOrEmpty(projectFilePath);
	}

	/// <summary>
	/// Finds the project named after the solution anywhere below the solution directory, which covers
	/// layouts such as <c>src/&lt;Name&gt;/&lt;Name&gt;.csproj</c>.
	/// </summary>
	/// <param name="solutionDir">The directory holding the solution file.</param>
	/// <param name="solutionName">The solution's file name without its extension.</param>
	/// <returns>The project's full path, or an empty string when there is no such project or more
	/// than one, since picking between them would be a guess.</returns>
	internal static string FindProjectNamedAfterSolutionBelow(string solutionDir, string solutionName)
	{
		string[] matches = FindFilesBelow(solutionDir, $"{solutionName}.csproj");
		return matches.Length == 1 ? Path.GetFullPath(matches[0]) : string.Empty;
	}
}

internal class DocumentCommand : CodeReviewCommand
{
	internal override Request TaskRequest
	{
		get
		{
			Request taskRequest = new()
			{
				Name = "Generate Documentation",
				Description = "Add documentation comments to these code files.",
			};

			if (!FindProjectAndSolutionFilePaths(Directory.GetCurrentDirectory(), out _, out string projectFilePath))
			{
				throw new InvalidOperationException("Could not find project and solution files.");
			}

			IEnumerable<string> filePaths = FindCSCodeBelow(projectFilePath);
			foreach (string filePath in filePaths)
			{
				taskRequest.Files.Add(new FileDefinition()
				{
					FilePath = filePath,
					Purpose = "C# Code File",
					Contents = File.ReadAllText(filePath),
				});
			}

			return taskRequest;
		}
	}
}

internal partial class TestCommand : CodeReviewCommand
{
	internal override Request TaskRequest
	{
		get
		{
			Request taskRequest = new()
			{
				Name = "Generate Code Tests",
				Description = @"
**Task:**

Generate **comprehensive and maintainable unit tests** for the provided C# classes. The tests should:

- Target **.NET 8** and **.NET 9** frameworks.
- Utilize features from **C# 13.0**, including modern constructs like records, enhanced pattern matching, nullable reference types, target-typed `new` expressions, and collection expressions.
- Be written using the **MSTest** framework.
- Cover all properties and methods thoroughly, including normal scenarios, edge cases, and error handling.
- Follow strict coding conventions and standards outlined below.

---

**Output Requirements:**

1. **Deliverable Format:**
   - Provide the test class as **fully executable C# code**.
   - Include necessary `using` directives (but exclude `using Microsoft.VisualStudio.TestTools.UnitTesting;` as it is preconfigured).
   - Write a single, cohesive test class unless otherwise specified.
   - Do not use underscores in test names (for example, use `MyMethodNullInputShouldThrowArgumentNullException` instead of `My_Method_Null_Input_Should_Throw_ArgumentNullException`).

2. **Code Style and Organization:**
   - Use **file-scoped namespaces**.
   - Place `using` directives **within** the namespace.
   - Follow **PascalCase** for public members and **camelCase** for private fields and local variables.
   - Use **var** for local variables unless explicit types improve readability.
   - Avoid **underscores** in identifiers.
   - Ensure clean formatting with tabs (size 4) and UTF-8 encoding, including a **final newline**.
   - Don't include using declarations for any namespaces that are already globally imported in the project.
   - Don't include any `using` directives for namespaces that are not used in the test class.

3. **Test Method Design:**
   - Use **descriptive test method names** that reflect the behavior being tested.
   - Organize methods into logical groups (e.g., **properties**, **methods**, **error conditions**).
   - Include **XML documentation comments** for each method describing its purpose and key scenarios covered.
   - Add inline comments for **complex logic** where necessary.

4. **Testing Guidelines:**
   - Validate properties for:
	 - Default values and initial states.
	 - Valid and invalid inputs, including **null**, **empty**, and **edge values**.
	 - Behavior of any applied **data annotations** or **custom validation attributes**.
   - Test methods for:
	 - All overloads and execution paths, including asynchronous scenarios.
	 - Exception handling using `Assert.ThrowsException<T>()` for verifying exception types and messages.
   - Use `[DataRow]` or `[DynamicData]` for data-driven tests when applicable.
   - Ensure all tests are **self-contained** and **independent**.
   - Mock dependencies using **Moq** or similar frameworks to isolate the system under test.

5. **C# 13.0 Features:**
   - Use modern features to simplify and improve test clarity, such as:
	 - Enhanced pattern matching.
	 - Target-typed `new`.
	 - Nullable reference types with warnings resolved.

6. **Additional Considerations:**
   - Include `[TestInitialize]` and `[TestCleanup]` methods if needed for setup and teardown.
   - Verify thread safety if applicable, using multi-threaded test scenarios.
   - Incorporate localization tests using `CultureInfo` where relevant.
   - Treat all code analysis warnings as errors and resolve them.

7. **Documentation:**
   - Write clear XML comments for each test method, summarizing:
	 - What is being tested.
	 - Why the test is necessary.
	 - Expected outcomes.

8. **Best Practices:**
   - Avoid **magic numbers** or **hardcoded strings**—use constants or enums.
   - Ensure maintainable test logic by refactoring repetitive code into helper methods.

---

**Example Output:**

```csharp
namespace MyNamespace.Tests
{
	using System;
	using Moq;

	[TestClass]
	public class MyClassTests
	{
		[TestMethod]
		/// <summary>
		/// Validates that the default value of MyProperty is correctly set.
		/// </summary>
		public void MyPropertyDefaultValueShouldBeExpectedValue()
		{
			// Arrange
			var instance = new MyClass();

			// Act
			var result = instance.MyProperty;

			// Assert
			Assert.AreEqual(expected: ""DefaultValue"", actual: result);
		}

		[TestMethod]
		/// <summary>
		/// Verifies that the MyMethod correctly handles null input.
		/// </summary>
		public void MyMethodNullInputShouldThrowArgumentNullException()
		{
			// Arrange
			var instance = new MyClass();

			// Act & Assert
			Assert.ThrowsException<ArgumentNullException>(() => instance.MyMethod(null));
		}
	}
}
```",
			};

			if (!FindProjectAndSolutionFilePaths(Directory.GetCurrentDirectory(), out string solutionFilePath, out _))
			{
				throw new InvalidOperationException("Could not find project and solution files.");
			}

			IEnumerable<string> filePaths = FindCSCodeBelow(solutionFilePath);
			foreach (string filePath in filePaths)
			{
				taskRequest.Files.Add(new FileDefinition()
				{
					FilePath = filePath,
					Purpose = "C# Code File",
					Contents = File.ReadAllText(filePath),
				});
			}

			string editorConfigFilePath = FindFileAbove(solutionFilePath, ".editorconfig");
			if (!string.IsNullOrEmpty(editorConfigFilePath))
			{
				taskRequest.Files.Add(new FileDefinition()
				{
					FilePath = editorConfigFilePath,
					Purpose = "EditorConfig File",
					Contents = File.ReadAllText(editorConfigFilePath),
				});
			}

			return taskRequest;
		}
	}

	/// <summary>
	/// The MSTest.Sdk version a scaffolded test project asks for when no <c>global.json</c> above the
	/// solution pins one.
	/// </summary>
	internal const string MSTestSdkVersion = "4.3.3";

	internal override void Setup(Settings settings)
	{
		base.Setup(settings);
		ScaffoldTestProject(Directory.GetCurrentDirectory());
	}

	/// <summary>
	/// Adds a test project for the project found from <paramref name="path"/> to its solution, unless
	/// the solution already has one.
	/// </summary>
	/// <param name="path">The directory or file to start the project and solution search from.</param>
	internal static void ScaffoldTestProject(string path)
	{
		if (!FindProjectAndSolutionFilePaths(path, out string solutionFilePath, out string projectFilePath))
		{
			throw new InvalidOperationException("Could not find project and solution files.");
		}

		string solutionDir = Path.GetDirectoryName(solutionFilePath) ?? string.Empty;
		string projectName = Path.GetFileNameWithoutExtension(projectFilePath);
		string testProjectName = $"{projectName}.Test";
		string testProjectDir = Path.Join(solutionDir, testProjectName);
		string testProjectFilePath = Path.Join(testProjectDir, $"{testProjectName}.csproj");
		string testClassName = $"{projectName}Tests";
		string testFileName = $"{testClassName}.cs";
		string testFilePath = Path.Join(testProjectDir, testFileName);

		Directory.CreateDirectory(testProjectDir);
		if (!File.Exists(testProjectFilePath))
		{
			File.WriteAllText(testProjectFilePath, TestProjectContent(projectFilePath, testProjectDir, IsMSTestSdkPinned(solutionDir)));
		}

		if (!File.Exists(testFilePath))
		{
			File.WriteAllText(testFilePath, $"namespace ktsu.{testProjectName};\r\n\r\n[TestClass]\r\npublic class {testClassName}\r\n{{\r\n}}\r\n");
		}

		// add the test project to the solution, matching its exact path so that a sibling whose name
		// merely starts with the same text (Sample.Tests, Sample.TestHelpers) does not count as it
		string solutionContent = File.ReadAllText(solutionFilePath);
		if (!solutionContent.Contains($"\"{testProjectName}\\{testProjectName}.csproj\"", StringComparison.OrdinalIgnoreCase))
		{
			string projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
			string csprojGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
			string projectEntry = $"Project(\"{csprojGuid}\") = \"{testProjectName}\", \"{testProjectName}\\{testProjectName}.csproj\", \"{projectGuid}\"\nEndProject";
			solutionContent = InsertProjectEntry(solutionContent, projectEntry);
			solutionContent = AddProjectConfigurations(solutionContent, projectGuid);
			File.WriteAllText(solutionFilePath, solutionContent);
		}
	}

	/// <summary>
	/// Builds a test project file that builds as written.
	/// </summary>
	/// <remarks>
	/// MSTest.Sdk brings the test framework and the adapter; the target framework and the reference to
	/// the project under test are the two things it cannot know. The MSTest namespace is imported by
	/// the project rather than left to MSTest.Sdk, which only does so under <c>ImplicitUsings</c>, and
	/// the generation prompt tells the model it is already there. A bare <c>Microsoft.NET.Sdk</c>
	/// project with none of this failed the solution's build on its first line.
	/// </remarks>
	/// <param name="projectFilePath">The project under test.</param>
	/// <param name="testProjectDir">The directory the test project is written to.</param>
	/// <param name="msTestSdkPinned">
	/// Whether a <c>global.json</c> already pins MSTest.Sdk, in which case the project names it without
	/// a version so the pin decides.
	/// </param>
	/// <returns>The test project file's content, with Windows newlines.</returns>
	internal static string TestProjectContent(string projectFilePath, string testProjectDir, bool msTestSdkPinned)
	{
		string sdk = msTestSdkPinned ? "MSTest.Sdk" : $"MSTest.Sdk/{MSTestSdkVersion}";
		string targetFramework = TestTargetFramework(File.ReadAllText(projectFilePath));
		string reference = Path.GetRelativePath(testProjectDir, projectFilePath).Replace('/', '\\');

		return $"<Project Sdk=\"{sdk}\">\r\n  <PropertyGroup>\r\n    <TargetFramework>{targetFramework}</TargetFramework>\r\n  </PropertyGroup>\r\n\r\n  <ItemGroup>\r\n    <ProjectReference Include=\"{reference}\" />\r\n    <Using Include=\"Microsoft.VisualStudio.TestTools.UnitTesting\" />\r\n  </ItemGroup>\r\n</Project>\r\n";
	}

	/// <summary>
	/// Chooses the framework a test project for the given project should target.
	/// </summary>
	/// <remarks>
	/// The first framework the project declares, so the reference resolves. A <c>netstandard</c>
	/// library cannot host a test run, and a project that inherits its frameworks from an SDK declares
	/// none; both get the framework this tool is running on, which the machine is known to have.
	/// </remarks>
	/// <param name="projectContent">The project file's content.</param>
	/// <returns>A target framework moniker.</returns>
	internal static string TestTargetFramework(string projectContent)
	{
		string? declared = TargetFrameworkPattern().Matches(projectContent)
			.SelectMany(match => match.Groups["frameworks"].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.FirstOrDefault(framework => !framework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase));

		return declared ?? $"net{Environment.Version.Major}.0";
	}

	/// <summary>
	/// Whether a <c>global.json</c> at or above the solution directory pins MSTest.Sdk.
	/// </summary>
	/// <param name="solutionDir">The directory holding the solution file.</param>
	/// <returns><see langword="true"/> when an <c>msbuild-sdks</c> entry names MSTest.Sdk.</returns>
	internal static bool IsMSTestSdkPinned(string solutionDir)
	{
		string globalJsonPath = FindFileAbove(solutionDir, "global.json");
		return !string.IsNullOrEmpty(globalJsonPath)
			&& File.ReadAllText(globalJsonPath).Contains("\"MSTest.Sdk\"", StringComparison.Ordinal);
	}

	[GeneratedRegex(@"<TargetFrameworks?>(?<frameworks>[^<]*)</TargetFrameworks?>")]
	private static partial Regex TargetFrameworkPattern();
}
