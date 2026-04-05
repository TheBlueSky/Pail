using Amazon.Runtime.CredentialManagement;

namespace Pail.Services;

public sealed class AwsProfileService : IAwsProfileService
{
	public Task<IReadOnlyList<string>> GetProfileNamesAsync()
	{
		var profileNames = new CredentialProfileStoreChain()
			.ListProfiles()
			.Where(profile => profile.CanCreateAWSCredentials)
			.Select(profile => profile.Name)
			.Distinct(IAwsProfileService.ProfileNameComparer)
			.OrderBy(name => name, IAwsProfileService.ProfileNameComparer)
			.ToArray();

		return Task.FromResult<IReadOnlyList<string>>(profileNames);
	}
}
