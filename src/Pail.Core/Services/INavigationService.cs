namespace Pail.Services;

public interface INavigationService
{
	public void NavigateTo(string pageKey, object? parameter = null);

	public void GoBack();
}
