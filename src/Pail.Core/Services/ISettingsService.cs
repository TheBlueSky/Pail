using Pail.Models;

namespace Pail.Services;

public interface ISettingsService
{
	public AppSettings Settings { get; }

	public Task LoadAsync(CancellationToken cancellationToken = default);

	public Task SaveAsync(CancellationToken cancellationToken = default);
}
