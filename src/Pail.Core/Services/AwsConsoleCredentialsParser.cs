using Pail.Models;

namespace Pail.Services;

public sealed class AwsConsoleCredentialsParser : IAwsConsoleCredentialsParser
{
	private const string AccessKeyIdKey = "aws_access_key_id";
	private const string SecretAccessKeyKey = "aws_secret_access_key";
	private const string SessionTokenKey = "aws_session_token";

	private static readonly HashSet<string> SupportedKeys =
	[
		AccessKeyIdKey,
		SecretAccessKeyKey,
		SessionTokenKey,
	];

	public AwsConsoleCredentials? Parse(string credentialsText)
	{
		if (string.IsNullOrWhiteSpace(credentialsText))
		{
			return null;
		}

		var lines = credentialsText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var parseState = ParseState.Initial;

		foreach (var line in lines)
		{
			if (IsHeaderLine(line))
			{
				if (parseState is not ParseState.Initial)
				{
					return null;
				}

				parseState = ParseState.HeaderSeen;
				continue;
			}

			var separatorIndex = line.IndexOf('=');

			if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
			{
				return null;
			}

			parseState = ParseState.CredentialLineSeen;

			var key = line[..separatorIndex].Trim();
			var value = line[(separatorIndex + 1)..].Trim();

			if (!SupportedKeys.Contains(key) || string.IsNullOrWhiteSpace(value) || !values.TryAdd(key, value))
			{
				return null;
			}
		}

		return values.Count == SupportedKeys.Count ?
			new AwsConsoleCredentials(values[AccessKeyIdKey], values[SecretAccessKeyKey], values[SessionTokenKey]) :
			null;
	}

	private static bool IsHeaderLine(string line) =>
		line.StartsWith('[') &&
		line.EndsWith(']') &&
		line.Length > 2;

	private enum ParseState
	{
		Initial,
		HeaderSeen,
		CredentialLineSeen,
	}
}
