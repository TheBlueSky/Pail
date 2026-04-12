namespace Pail.Services;

public interface IFolderPickerService
{
	public Task<string?> PickFolderAsync(string? suggestedPath = null, CancellationToken cancellationToken = default);
}
