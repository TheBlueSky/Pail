using Microsoft.UI.Dispatching;
using Pail.Services;

namespace Pail.App.Services;

public sealed class DispatcherService : IDispatcherService
{
	private readonly DispatcherQueue _dispatcherQueue;

	public DispatcherService(DispatcherQueue dispatcherQueue)
	{
		ArgumentNullException.ThrowIfNull(dispatcherQueue);
		_dispatcherQueue = dispatcherQueue;
	}

	public void Run(Action action)
	{
		ArgumentNullException.ThrowIfNull(action);

		if (_dispatcherQueue.HasThreadAccess)
		{
			action();
			return;
		}

		_dispatcherQueue.TryEnqueue(() => action());
	}
}
