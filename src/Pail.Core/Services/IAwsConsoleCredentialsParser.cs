using Pail.Models;

namespace Pail.Services;

public interface IAwsConsoleCredentialsParser
{
	public AwsConsoleCredentials? Parse(string credentialsText);
}
