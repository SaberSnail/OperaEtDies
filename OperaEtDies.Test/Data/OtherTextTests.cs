using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.Data;

/// <summary>
/// Text the player is shown that names the other person in what they're doing, with their character's text.
/// </summary>
[TestFixture]
public sealed class OtherTextTests
{
	[Test]
	public void OtherTextsAreFilledInFromTheOtherPersonsCharacter()
	{
		var gameData = CreateGameData();
		var random = new Random(0);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		var other = world.Npcs[0];
		var pronoun = other.Traits.GetOption("gender").Text["pronoun"];

		// The name, a template, and a property's text, with a format, and literal braces left alone.
		Assert.That(ActivityText.FillOther("{other} / {other.fullName} / {other.gender.pronoun:first_upper} {{x}}", other),
			Is.EqualTo($"{other.FullName} / {other.FullName} / {char.ToUpperInvariant(pronoun[0])}{pronoun[1..]} {{x}}"));

		// The player is "you", and without anyone to name, it's "someone".
		Assert.That(ActivityText.FillOther("{other}", world.Player), Is.EqualTo(OurResources.LogYouObject));
		Assert.That(ActivityText.FillOther("{other} and {other.gender.pronoun}", null), Is.EqualTo($"{OurResources.ActivityPartnerUnknown} and {OurResources.ActivityPartnerUnknown}"));
	}

	[Test]
	public void OtherTextsAreChecked()
	{
		var characters = CreateCharacterData();
		var properties = characters.Properties.ToDictionary(x => x.Id, StringComparer.Ordinal);
		var templateIds = characters.Templates.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		IReadOnlyList<string> Validate(string text) => OtherText.Validate("text", text, properties, templateIds).ToList();

		Assert.That(Validate("Ask whether {other.gender.pronoun} would like {other.fullName} to come. {{Braces}}"), Is.Empty);
		Assert.That(Validate("Ask {them}"), Is.EqualTo(new[] { OurResources.OtherTextPlaceholderUnknown.FormatCurrentCulture("text", "them") }));
		Assert.That(Validate("Ask {other"), Is.EqualTo(new[] { OurResources.TemplateMalformed.FormatCurrentCulture("text") }));
		Assert.That(Validate("Ask {other:shout}"), Is.EqualTo(new[] { OurResources.TemplateFormatUnknown.FormatCurrentCulture("text", "shout") }));
		Assert.That(Validate("Ask {other.unknown}"), Is.EqualTo(new[] { OurResources.TemplatePropertyUnknown.FormatCurrentCulture("text", "unknown") }));
		Assert.That(Validate("Ask {other.gender.object}"), Is.EqualTo(new[]
		{
			OurResources.TemplateFieldMissing.FormatCurrentCulture("text", "gender", "object", "male"),
			OurResources.TemplateFieldMissing.FormatCurrentCulture("text", "gender", "object", "female"),
		}));
	}
}
