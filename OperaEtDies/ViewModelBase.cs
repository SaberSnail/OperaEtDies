using GoldenAnvil.Utility.Windows;
using System.Windows.Threading;

namespace OperaEtDies;

public abstract class ViewModelBase : NotifyPropertyChangedDispatcherBase
{
	protected ViewModelBase()
	{
	}

	protected ViewModelBase(Dispatcher dispatcher)
		: base(dispatcher)
	{
	}
}
