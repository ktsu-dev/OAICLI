namespace ktsu.OAICLI;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

internal static class OAICLI
{
	/// <summary>
	/// Specifies the content types that can be used in messages.
	/// </summary>
	internal enum MessageContentType
	{
		Text,
	}

	/// <summary>
	/// Specifies the roles that can be assigned to messages.
	/// </summary>
	internal enum Role
	{
		Developer,
		User,
		Assistant,
	}

	/// <summary>
	/// Represents a message in the conversation.
	/// </summary>
	internal class Message
	{
		/// <summary>
		/// Gets or sets the role of the message sender.
		/// </summary>
		public Role Role { get; set; }

		/// <summary>
		/// Gets or sets the content of the message.
		/// </summary>
		public MessageContent[] Content { get; set; } = [];
	}

	/// <summary>
	/// Represents the content of a message.
	/// </summary>
	public class MessageContent
	{
		/// <summary>
		/// Gets or sets the type of the message content.
		/// </summary>
		public MessageContentType Type { get; set; }

		/// <summary>
		/// Gets or sets the text of the message content.
		/// </summary>
		public string Text { get; set; } = string.Empty;
	}

	/// <summary>
	/// Represents the body of a request to the OpenAI API.
	/// </summary>
	internal class RequestBody
	{
		/// <summary>
		/// Gets or sets the model to be used for the request.
		/// </summary>
		public string Model { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the messages to be sent in the request.
		/// </summary>
		public Message[] Messages { get; set; } = [];

		public ResponseFormat ResponseFormat { get; set; } = new();
	}

	/// <summary>
	/// Gets the JSON serializer options used for serialization.
	/// </summary>
	private static JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerOptions.Default)
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
		},
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		WriteIndented = true,
	};

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
			config.SetApplicationName(nameof(OAICLI));
			config.ValidateExamples();

			config.AddCommand<DocumentCommand>("document")
				.WithExample("document", "path/to/Program.cs");

			config.AddCommand<TestCommand>("test")
				.WithExample("test");
		});

		app.SetDefaultCommand<TestCommand>();

		return app.Run(args);
	}

	internal static string MakeRequest(TaskRequest taskRequest)
	{
		EnsureHasApiKey();

		using HttpClient client = new();
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppData.Get().ApiKey}");

		RequestBody requestBody = new()
		{
			Model = "gpt-4o",
			Messages =
			[
				new()
				{
					Role = Role.Developer,
					Content =
					[
						new()
						{
							Type = MessageContentType.Text,
							Text = "You are a helpful, expert coding assistant. You will be performing coding tasks which you will receive in a json format.",
						},
					],
				},
				new()
				{
					Role = Role.User,
					Content =
					[
						new()
						{
							Type = MessageContentType.Text,
							Text = JsonSerializer.Serialize(taskRequest, JsonSerializerOptions),
						},
					],
				},
			],
		};

		string requestJson = JsonSerializer.Serialize(requestBody, JsonSerializerOptions);
		AnsiConsole.Write(new Panel(new JsonText(requestJson)).BorderColor(Color.Green).Header("Request"));

		using StringContent content = new(requestJson, Encoding.UTF8, "application/json");
		Uri requestURI = new("https://api.openai.com/v1/chat/completions");
		var response = client.PostAsync(requestURI, content).Result;
		//response.EnsureSuccessStatusCode();

		string responseJson = response.Content.ReadAsStringAsync().Result;
		AnsiConsole.Write(new Panel(new JsonText(responseJson)).BorderColor(Color.Green).Header("Response"));
		return responseJson;
	}

	/// <summary>
	/// Ensures that an API key has been provided by the user.
	/// </summary>
	private static void EnsureHasApiKey()
	{
		var appData = AppData.Get();
		while (string.IsNullOrWhiteSpace(appData.ApiKey))
		{
			TextPrompt<string> textPrompt = new("Supply your OpenAI api key:");
			appData.ApiKey = AnsiConsole.Prompt(textPrompt);
			appData.Save();
		}
	}
}
