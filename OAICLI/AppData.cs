// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.OAICLI;

using ktsu.AppDataStorage;

internal class AppData : AppData<AppData>
{
	public string ApiKey { get; set; } = string.Empty;
}
