namespace ktsu.OAICLI;

using ktsu.AppDataStorage;

internal class AppData : AppData<AppData>
{
	public string ApiKey { get; set; } = string.Empty;
}
