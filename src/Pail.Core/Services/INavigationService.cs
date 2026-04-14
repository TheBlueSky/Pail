namespace Pail.Services;

public interface INavigationService
{
	public bool CanGoBack { get; }

	public void NavigateTo(string pageKey, object? parameter = null, bool clearBackStack = false);

	public void GoBack();
}
