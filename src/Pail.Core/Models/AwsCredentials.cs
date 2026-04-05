namespace Pail.Models;

public interface IAwsCredentials
{
	public string Region { get; }
}

public sealed record AwsSessionCredentials(string AccessKey, string SecretKey, string SessionToken, string Region) : IAwsCredentials;

public sealed record AwsDefaultChainCredentials(string? ProfileName, string Region) : IAwsCredentials;
