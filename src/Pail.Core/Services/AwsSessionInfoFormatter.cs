using Pail.Models;

namespace Pail.Services;

public static class AwsSessionInfoFormatter
{
	public static string FormatSummary(AwsSessionInfo? sessionInfo)
	{
		if (sessionInfo is null)
		{
			return string.Empty;
		}

		var parts = new[]
		{
			sessionInfo.AccountAlias,
			sessionInfo.ProfileName,
			sessionInfo.AccountId,
			sessionInfo.Region,
			FormatCaller(sessionInfo.CallerArn),
		};

		return string.Join(" | ", parts.Where(part => string.IsNullOrWhiteSpace(part) is false));
	}

	private static string? FormatCaller(string? callerArn)
	{
		if (string.IsNullOrWhiteSpace(callerArn))
		{
			return null;
		}

		var resourceSeparatorIndex = callerArn.LastIndexOf(':');
		var resource = resourceSeparatorIndex >= 0 && resourceSeparatorIndex < callerArn.Length - 1
			? callerArn[(resourceSeparatorIndex + 1)..]
			: callerArn;

		foreach (var prefix in new[] { "assumed-role/", "role/", "user/" })
		{
			if (resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return resource[prefix.Length..];
			}
		}

		return resource;
	}
}
