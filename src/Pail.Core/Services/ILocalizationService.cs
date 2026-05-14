namespace Pail.Services;

public interface ILocalizationService
{
	public string GetString(string resourceName, string fallback);

	public string FormatString(string resourceName, string fallbackFormat, params object?[] args);
}
