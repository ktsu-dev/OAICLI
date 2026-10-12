// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Net;
using System.Xml.Linq;

/// <summary>
/// Covers what <c>oai test</c> writes into the user's solution and when: nothing until the request
/// has succeeded, and then a test project that builds.
/// </summary>
[TestClass]
public sealed class TestCommandScaffoldTests
{
	/// <summary>
	/// The projects an <c>.slnx</c> listing only the project under test should list once scaffolded.
	/// </summary>
	private static readonly string[] SlnxProjectsAfterScaffolding = ["Sample/Sample.csproj", "Sample.Test/Sample.Test.csproj"];

	/// <summary>
	/// A scratch solution directory unique to the running test, removed during cleanup.
	/// </summary>
	private string solutionDirectory = string.Empty;

	/// <summary>
	/// Creates the scratch directory each test builds its solution inside.
	/// </summary>
	[TestInitialize]
	public void TestInitialize()
	{
		solutionDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		_ = Directory.CreateDirectory(solutionDirectory);
	}

	/// <summary>
	/// Removes the scratch directory so the tests stay self contained.
	/// </summary>
	[TestCleanup]
	public void TestCleanup()
	{
		if (Directory.Exists(solutionDirectory))
		{
			Directory.Delete(solutionDirectory, recursive: true);
		}
	}

	/// <summary>
	/// A rejected request has nothing to apply, so the command's changes must not be written.
	/// </summary>
	[TestMethod]
	public void RunDoesNotApplyTheResultWhenTheRequestFails()
	{
		bool applied = false;

		int exitCode = CodeReviewCommand.Run(
			() => throw new HttpRequestException("Unauthorized", inner: null, HttpStatusCode.Unauthorized),
			() => applied = true);

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
		Assert.IsFalse(applied, "The result was applied although the request failed");
	}

	/// <summary>
	/// A successful request is what the changes wait for.
	/// </summary>
	[TestMethod]
	public void RunAppliesTheResultAfterASuccessfulRequest()
	{
		bool applied = false;

		int exitCode = CodeReviewCommand.Run(() => "{}", () => applied = true);

		Assert.AreEqual(0, exitCode);
		Assert.IsTrue(applied, "The result was not applied after a successful request");
	}

	/// <summary>
	/// The scaffolded project names a versioned MSTest.Sdk, the framework of the project under test,
	/// a reference to it, and the MSTest namespace the scaffolded <c>[TestClass]</c> needs, so the
	/// solution still builds once it is added.
	/// </summary>
	[TestMethod]
	public void ScaffoldTestProjectWritesAProjectThatCanBuild()
	{
		WriteSolution("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");

		TestCommand.ScaffoldTestProject(solutionDirectory);

		string content = ReadTestProject();
		StringAssert.Contains(content, $"<Project Sdk=\"MSTest.Sdk/{TestCommand.MSTestSdkVersion}\">");
		StringAssert.Contains(content, "<TargetFramework>net8.0</TargetFramework>");
		StringAssert.Contains(content, "<ProjectReference Include=\"..\\Sample\\Sample.csproj\" />");
		StringAssert.Contains(content, "<Using Include=\"Microsoft.VisualStudio.TestTools.UnitTesting\" />");
	}

	/// <summary>
	/// A <c>global.json</c> that already pins MSTest.Sdk decides the version, so the project names the
	/// SDK without one rather than asking for a second version beside the pin.
	/// </summary>
	[TestMethod]
	public void ScaffoldTestProjectDefersToAPinnedMSTestSdk()
	{
		WriteSolution("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");
		File.WriteAllText(Path.Combine(solutionDirectory, "global.json"), """{ "msbuild-sdks": { "MSTest.Sdk": "4.3.3" } }""");

		TestCommand.ScaffoldTestProject(solutionDirectory);

		StringAssert.Contains(ReadTestProject(), "<Project Sdk=\"MSTest.Sdk\">");
	}

	/// <summary>
	/// The test project targets the first framework the project under test declares, unless that is a
	/// <c>netstandard</c> one or there is none, in which case it targets the running framework.
	/// </summary>
	/// <param name="projectContent">The project under test's file content.</param>
	/// <param name="expected">The framework expected, or an empty string for the running one.</param>
	[TestMethod]
	[DataRow("<TargetFramework>net8.0</TargetFramework>", "net8.0", DisplayName = "Single framework")]
	[DataRow("<TargetFrameworks>net9.0;net8.0</TargetFrameworks>", "net9.0", DisplayName = "First of several")]
	[DataRow("<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>", "net8.0", DisplayName = "Skips netstandard")]
	[DataRow("<TargetFramework>netstandard2.1</TargetFramework>", "", DisplayName = "Only netstandard")]
	[DataRow("<TargetFrameworks></TargetFrameworks>", "", DisplayName = "Inherited from an SDK")]
	public void TestTargetFrameworkFollowsTheProjectUnderTest(string projectContent, string expected)
	{
		string running = $"net{Environment.Version.Major}.0";

		Assert.AreEqual(string.IsNullOrEmpty(expected) ? running : expected, TestCommand.TestTargetFramework(projectContent));
	}

	/// <summary>
	/// An <c>.slnx</c> solution is XML, so the test project goes in as a <c>Project</c> element rather
	/// than the <c>.sln</c> text block, and running the command again does not add it a second time.
	/// </summary>
	[TestMethod]
	public void ScaffoldTestProjectRegistersTheTestProjectInAnSlnxOnce()
	{
		WriteSolution("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n", "Sample.slnx", "<Solution>\n  <Project Path=\"Sample/Sample.csproj\" />\n</Solution>\n");

		TestCommand.ScaffoldTestProject(solutionDirectory);
		TestCommand.ScaffoldTestProject(solutionDirectory);

		XDocument solution = XDocument.Load(Path.Combine(solutionDirectory, "Sample.slnx"));
		string[] paths = [.. solution.Descendants("Project").Select(project => (string?)project.Attribute("Path") ?? string.Empty)];
		CollectionAssert.AreEqual(SlnxProjectsAfterScaffolding, paths);
	}

	/// <summary>
	/// The new element goes in last, on its own line, indented like its siblings and using the file's
	/// own line endings.
	/// </summary>
	[TestMethod]
	public void AddProjectToSlnxAppendsAnElementInTheFilesOwnStyle()
	{
		const string solution = "<Solution>\r\n  <Project Path=\"Sample/Sample.csproj\" />\r\n</Solution>\r\n";

		string result = TestCommand.AddProjectToSlnx(solution, "Sample.Test/Sample.Test.csproj");

		Assert.AreEqual("<Solution>\r\n  <Project Path=\"Sample/Sample.csproj\" />\r\n  <Project Path=\"Sample.Test/Sample.Test.csproj\" />\r\n</Solution>\r\n", result);
	}

	/// <summary>
	/// A solution with no projects can be written as one self-closing element, which has no closing
	/// tag to insert in front of.
	/// </summary>
	[TestMethod]
	public void AddProjectToSlnxOpensASelfClosingSolution()
	{
		string result = TestCommand.AddProjectToSlnx("<Solution />\n", "Sample.Test/Sample.Test.csproj");

		Assert.AreEqual("<Solution>\n  <Project Path=\"Sample.Test/Sample.Test.csproj\" />\n</Solution>\n", result);
	}

	/// <summary>
	/// A test project already listed, whether inside a solution folder or written with backslashes,
	/// counts as present.
	/// </summary>
	/// <param name="solution">The solution's content.</param>
	[TestMethod]
	[DataRow("<Solution>\n  <Folder Name=\"/tests/\">\n    <Project Path=\"Sample.Test/Sample.Test.csproj\" />\n  </Folder>\n</Solution>\n", DisplayName = "Inside a folder")]
	[DataRow("<Solution>\n  <Project Path=\"Sample.Test\\Sample.Test.csproj\" />\n</Solution>\n", DisplayName = "Backslashes")]
	[DataRow("<Solution>\n  <Project Path=\"sample.test/sample.test.csproj\" />\n</Solution>\n", DisplayName = "Different case")]
	public void AddProjectToSlnxLeavesAListedProjectAlone(string solution) =>
		Assert.AreEqual(solution, TestCommand.AddProjectToSlnx(solution, "Sample.Test/Sample.Test.csproj"));

	/// <summary>
	/// Writes a solution and the <c>Sample</c> project it is named after.
	/// </summary>
	/// <param name="projectContent">The project file's content.</param>
	/// <param name="solutionFileName">The solution's file name, <c>Sample.sln</c> unless given.</param>
	/// <param name="solutionContent">The solution's content, an empty <c>.sln</c> unless given.</param>
	private void WriteSolution(string projectContent, string solutionFileName = "Sample.sln", string solutionContent = "Microsoft Visual Studio Solution File, Format Version 12.00\nGlobal\nEndGlobal\n")
	{
		File.WriteAllText(Path.Combine(solutionDirectory, solutionFileName), solutionContent);
		string projectDirectory = Path.Combine(solutionDirectory, "Sample");
		_ = Directory.CreateDirectory(projectDirectory);
		File.WriteAllText(Path.Combine(projectDirectory, "Sample.csproj"), projectContent);
	}

	/// <summary>
	/// Reads back the test project the scaffold wrote.
	/// </summary>
	/// <returns>The test project file's content.</returns>
	private string ReadTestProject() =>
		File.ReadAllText(Path.Combine(solutionDirectory, "Sample.Test", "Sample.Test.csproj"));
}
