namespace Pail.Services;

public interface IAwsProfileService
{
	public static readonly StringComparer ProfileNameComparer = StringComparer.OrdinalIgnoreCase;

	public Task<IReadOnlyList<string>> GetProfileNamesAsync();
}
