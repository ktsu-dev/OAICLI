namespace ktsu.OAICLI;

using System.Collections.ObjectModel;
using System.Text.Json;

internal class Request
{
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Collection<FileDefinition> Files { get; set; } = [];

	internal string Send()
	{
		Auth.EnsureHasApiKey();

		using HttpClient client = new();
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppData.Get().ApiKey}");

		RequestBody requestBody = new()
		{
			Model = "gpt-4o",
			Messages =
			[
				new()
				{
					Role = MessageRole.Developer,
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
					Role = MessageRole.User,
					Content =
					[
						new()
						{
							Type = MessageContentType.Text,
							Text = JsonSerializer.Serialize(this, Json.SerializerOptions),
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
