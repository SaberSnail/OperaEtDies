using System.Reflection;
using ModEditor.ViewModels;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class ModEditorCommandTests
{
	/// <summary>
	/// The window binds its commands to the view model's methods by name, which fails if a name is overloaded.
	/// </summary>
	[Test]
	public void PublicMethodNamesAreUnique()
	{
		var overloaded = typeof(MainWindowViewModel).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.GroupBy(x => x.Name)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key);

		Assert.That(overloaded, Is.Empty);
	}
}
