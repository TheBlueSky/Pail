namespace Pail.Services;

public interface IFileManagerService
{
	public Task<bool> ShowInFileManagerAsync(string path, CancellationToken cancellationToken = default);
}
