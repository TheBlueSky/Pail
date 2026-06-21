using Pail.Models;

namespace Pail.Services;

public interface IAwsIdentityService
{
	public Task<AwsCallerIdentity?> TryGetCallerIdentityAsync(IAwsCredentials credentials, CancellationToken cancellationToken = default);

	public Task<string?> TryGetAccountAliasAsync(IAwsCredentials credentials, CancellationToken cancellationToken = default);
}
