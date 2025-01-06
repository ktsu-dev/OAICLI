namespace ktsu.OAICLI;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using ktsu.Extensions;
using Spectre.Console.Cli;

internal abstract class CodeReviewCommand : Command<CodeReviewCommand.Settings>
{
	internal abstract TaskRequest TaskRequest { get; }

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


	private static string FileBeginTag => $"##{nameof(OAICLI)}_FILE_BEGIN##";
	private static string FileEndTag => $"##{nameof(OAICLI)}_FILE_END##";

	private static string TaskBeginTag => $"##{nameof(OAICLI)}_TASK_BEGIN##";
	private static string TaskEndTag => $"##{nameof(OAICLI)}_TASK_END##";

	private static string ContextBeginTag => $"##{nameof(OAICLI)}_CONTEXT_BEGIN##";
	private static string ContextEndTag => $"##{nameof(OAICLI)}_CONTEXT_END##";

	private static string ResponseBeginTag => $"##{nameof(OAICLI)}_RESPONSE_BEGIN##";
	private static string ResponseEndTag => $"##{nameof(OAICLI)}_RESPONSE_END##";

	public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
	{
		Setup(settings);

		//string responseJson = OAICLI.MakeRequest(TaskRequest);
		OAICLI.MakeRequest(TaskRequest);
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

		return 0;
	}

	internal static string EnsureTrainingNewLine(string contentOriginal, string contentModified)
	{
		var lineEndingsOriginal = contentOriginal.DetermineLineEndings();
		contentModified = contentModified.NormalizeLineEndings(LineEndingStyle.Unix);
		contentModified = contentModified.Trim();
		contentModified += "\n";

		contentModified = contentModified.NormalizeLineEndings(lineEndingsOriginal);
		return contentModified;
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
		Collection<string> files = [];
		if (!string.IsNullOrWhiteSpace(currentDir))
		{
			files.AddMany(Directory.GetFiles(currentDir, pattern, SearchOption.AllDirectories));
		}
		return [.. files];
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
			projectFilePath = FindCSProjAbove(projectDir);
		}

		return !string.IsNullOrEmpty(solutionFilePath) && !string.IsNullOrEmpty(projectFilePath);
	}
}

internal class DocumentCommand : CodeReviewCommand
{
	internal override TaskRequest TaskRequest
	{
		get
		{
			TaskRequest taskRequest = new()
			{
				Name = "Generate Documentation",
				Description = "Add documentation comments to these code files.",
			};

			if (!FindProjectAndSolutionFilePaths(Directory.GetCurrentDirectory(), out string _, out string projectFilePath))
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

internal class TestCommand : CodeReviewCommand
{
	internal override TaskRequest TaskRequest
	{
		get
		{
			TaskRequest taskRequest = new()
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

			if (!FindProjectAndSolutionFilePaths(Directory.GetCurrentDirectory(), out string solutionFilePath, out string _))
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

	internal override void Setup(Settings settings)
	{
		base.Setup(settings);

		if (!FindProjectAndSolutionFilePaths(Directory.GetCurrentDirectory(), out string solutionFilePath, out string projectFilePath))
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
			File.WriteAllText(testProjectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n  <PropertyGroup>\r\n    <IsTestProject>true</IsTestProject>\r\n  </PropertyGroup>\r\n</Project>\r\n");
		}

		if (!File.Exists(testFilePath))
		{
			File.WriteAllText(testFilePath, $"namespace ktsu.{testProjectName};\r\n\r\n[TestClass]\r\npublic class {testClassName}\r\n{{\r\n}}\r\n");
		}

		// add the test project to the solution
		string solutionContent = File.ReadAllText(solutionFilePath);
		if (!solutionContent.Contains(testProjectName))
		{
			string projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
			string csprojGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
			string projectContent = $"\r\nProject(\"{csprojGuid}\") = \"{testProjectName}\", \"{testProjectName}\\{testProjectName}.csproj\", \"{{{projectGuid}}}\"\r\nEndProject";
			solutionContent = solutionContent.Replace("EndProject", projectContent);
			File.WriteAllText(solutionFilePath, solutionContent);
		}
	}
}
