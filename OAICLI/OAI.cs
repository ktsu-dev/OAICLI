namespace ktsu.OAICLI;

using Spectre.Console.Cli;

internal static partial class OAI
{
	internal const string DeveloperPrompt = "You are a helpful, expert coding assistant. You will be performing coding tasks which you will receive in a json format.";

	/// <summary>
	/// The entry point for the command-line application.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The exit code for the application.</returns>
	private static int Main(string[] args)
	{
		var app = new CommandApp();
		app.Configure(config =>
		{
			_ = config.SetApplicationName(nameof(OAI));
			_ = config.ValidateExamples();

			_ = config.AddCommand<DocumentCommand>("document")
				.WithExample("document", "path/to/Program.cs");

			_ = config.AddCommand<TestCommand>("test")
				.WithExample("test");
		});

		app.SetDefaultCommand<TestCommand>();

		return app.Run(args);
	}
}
