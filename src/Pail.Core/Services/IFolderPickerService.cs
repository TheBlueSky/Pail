namespace Pail.Services;

public interface IFolderPickerService
{
	public Task<string?> PickFolderAsync();
}
