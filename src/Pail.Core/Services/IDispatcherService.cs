namespace Pail.Services;

public interface IDispatcherService
{
	public void Run(Action action);
}
