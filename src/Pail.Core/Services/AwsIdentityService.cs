using Amazon.IdentityManagement.Model;
using Amazon.SecurityToken.Model;
using Pail.Models;

namespace Pail.Services;

public sealed class AwsIdentityService : IAwsIdentityService
{
	private readonly IAwsClientFactory _awsClientFactory;

	public AwsIdentityService(IAwsClientFactory awsClientFactory)
	{
		ArgumentNullException.ThrowIfNull(awsClientFactory);

		_awsClientFactory = awsClientFactory;
	}

	public async Task<AwsCallerIdentity?> TryGetCallerIdentityAsync(IAwsCredentials credentials, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(credentials);

		try
		{
			using var client = _awsClientFactory.CreateSecurityTokenServiceClient(credentials);
			var response = await client.GetCallerIdentityAsync(new GetCallerIdentityRequest(), cancellationToken);

			return new AwsCallerIdentity(response.Account, response.Arn);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return null;
		}
	}

	public async Task<string?> TryGetAccountAliasAsync(IAwsCredentials credentials, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(credentials);

		try
		{
			using var client = _awsClientFactory.CreateIdentityManagementServiceClient(credentials);
			var response = await client.ListAccountAliasesAsync(new ListAccountAliasesRequest(), cancellationToken);

			return response.AccountAliases.FirstOrDefault(alias => string.IsNullOrWhiteSpace(alias) is false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return null;
		}
	}
}
