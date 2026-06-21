namespace Pail.Models;

public sealed record AwsSessionInfo(
	string Region,
	string? ProfileName,
	string? AccountId,
	string? CallerArn,
	string? AccountAlias);
