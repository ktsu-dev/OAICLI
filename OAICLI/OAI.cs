// Copyright (c) 2023-2026 ktsu-dev contributors

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
		CommandApp app = new();
		app.Configure(Configure);

		app.SetDefaultCommand<TestCommand>();

		return app.Run(args);
	}

	/// <summary>
	/// Registers the commands and their examples.
	/// </summary>
	/// <param name="config">The configurator to register them with.</param>
	internal static void Configure(IConfigurator config)
	{
		_ = config.SetApplicationName(nameof(OAI));
		_ = config.ValidateExamples();

		// Without strict parsing an unknown option is set aside as a remaining argument and the
		// command runs anyway, which is how --context and --force went unnoticed.
		_ = config.UseStrictParsing();

		_ = config.AddCommand<DocumentCommand>("document")
			.WithExample("document");

		_ = config.AddCommand<TestCommand>("test")
			.WithExample("test");
	}
}
