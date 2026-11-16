using ModEditor.Model;

namespace OperaEtDies.Test.Tools;

/// <summary>
/// Checks that the help built into the mod editor explains every part of the data the form can show, so a field's
/// help always has somewhere to go, and DataFiles.md stays complete.
/// </summary>
[TestFixture]
public sealed class ModEditorHelpTests
{
	[Test]
	public void TheDocumentsAreBuiltIn()
	{
		Assert.That(HelpLibrary.Documents.Select(x => x.Name), Is.EqualTo(new[] { HelpLibrary.ModEditor, HelpLibrary.DataFiles, HelpLibrary.Mods }));
		Assert.That(HelpLibrary.Documents.All(x => x.Headings.Count != 0), Is.True);
	}

	[Test]
	public void EveryClassOfTheDataHasAHeading()
	{
		var missing = GetDataTypes().Where(x => HelpTopics.ForType(x) is null).Select(x => $"{x.Name} (\"{HelpTopics.GetHeading(x)}\")").ToList();

		Assert.That(missing, Is.Empty);
	}

	[Test]
	public void EveryPropertyTheGameReadsHasARow()
	{
		var document = HelpLibrary.Find(HelpLibrary.DataFiles)!;
		var missing = GetDataTypes()
			.SelectMany(type => DataSchema.GetFields(type).Select(field => (type, field)))
			.Where(x => !document.HasRow(HelpTopics.GetHeading(x.type), x.field.Name))
			.Select(x => $"{HelpTopics.GetHeading(x.type)}: {x.field.Name}")
			.ToList();

		Assert.That(missing, Is.Empty);
	}

	[Test]
	public void EverySectionHasARow()
	{
		var missing = DataSchema.ListSections.Keys.Concat(DataSchema.SettingsSections.Keys).Where(x => HelpTopics.ForSection(x)?.Row is null).ToList();

		Assert.That(missing, Is.Empty);
	}

	[TestCase("op")]
	[TestCase("path")]
	[TestCase("value")]
	[TestCase("values")]
	[TestCase("changes")]
	[TestCase("index")]
	[TestCase("before")]
	[TestCase("after")]
	public void EveryPropertyOfAnOperationHasARow(string property)
	{
		Assert.That(HelpTopics.ForOperation(property)?.Row, Is.EqualTo(property));
	}

	[Test]
	public void RowsHaveTheirNotes()
	{
		Assert.That(HelpLibrary.GetRowText(HelpTopics.ForProperty(typeof(OperaEtDies.Data.CharacterProperty), "follows")), Does.StartWith("string — Optional."));
	}

	/// <summary>
	/// Gets every class the form can show, by following the properties of the sections' classes.
	/// </summary>
	private static IReadOnlyList<Type> GetDataTypes()
	{
		var found = new HashSet<Type>();
		void Visit(DataType? type)
		{
			if (type is null)
				return;
			if (type.Kind is DataKind.List or DataKind.Map)
			{
				Visit(type.Element);
			}
			else if (type.Kind == DataKind.Object && found.Add(type.ClrType))
			{
				foreach (var field in type.Fields)
					Visit(field.Type);
			}
		}

		foreach (var type in DataSchema.ListSections.Values.Concat(DataSchema.SettingsSections.Values))
			Visit(DataSchema.GetType(type));
		return found.ToList();
	}
}
