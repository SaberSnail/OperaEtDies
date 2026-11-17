using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Characters;

[TestFixture]
public sealed class ClothingPlaceholderTests
{
	[Test]
	public void TheClothingPlaceholderIsFilledInAndHiddenWhenNothingIsWorn()
	{
		var characters = CreateCharacters(
			new CharacterTemplateVariant { When = new CharacterCondition { Hidden = [CharacterData.ClothingPlaceholderId] }, Template = "Hugh stands here." },
			new CharacterTemplateVariant { Template = "Hugh wears {clothing}." });
		Assert.That(characters.Validate(), Is.Empty);

		var gameData = new GameData(characters, TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(), TestData.CreateRelationshipSettings(), TestData.CreateCalendarSettings());
		var traits = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc).GetTraits();
		Assert.That(traits.GetDescription(gameData.CharacterTemplates, null, "a hat and a cloak"), Is.EqualTo("Hugh wears a hat and a cloak."));
		Assert.That(traits.GetDescription(gameData.CharacterTemplates, null, ""), Is.EqualTo("Hugh stands here."));
		Assert.That(traits.GetDescription(gameData.CharacterTemplates), Is.EqualTo("Hugh stands here."));
	}

	[Test]
	public void TheClothingPlaceholderNeedsAVariantForWhenNothingIsWorn()
	{
		var characters = CreateCharacters(new CharacterTemplateVariant { Template = "Hugh wears {clothing}." });

		Assert.That(characters.Validate(), Is.Not.Empty.And.All.Contains("\"clothing\""));
	}

	[Test]
	public void TheClothingPlaceholderHasNoFieldsAndIsReserved()
	{
		var characters = CreateCharacters(
			new CharacterTemplateVariant { When = new CharacterCondition { Hidden = [CharacterData.ClothingPlaceholderId] }, Template = "" },
			new CharacterTemplateVariant { Template = "{clothing.name}" });
		characters = new CharacterData { Templates = [.. characters.Templates, new CharacterTemplate { Id = "clothing", Template = "x" }], Properties = characters.Properties };

		Assert.That(characters.Validate(), Is.SupersetOf(new[]
		{
			OurResources.TemplateFieldNotAllowed.FormatCurrentCulture("description.variants[1]", "clothing", "name"),
			OurResources.CharacterIdReserved.FormatCurrentCulture("clothing"),
		}));
	}

	[Test]
	public void ListsAreJoinedWithCommasAndAnd()
	{
		Assert.That(TextList.Join([]), Is.EqualTo(""));
		Assert.That(TextList.Join(["a hat"]), Is.EqualTo("a hat"));
		Assert.That(TextList.Join(["a hat", "a cloak"]), Is.EqualTo("a hat and a cloak"));
		Assert.That(TextList.Join(["a hat", "a cloak", "a tunic"]), Is.EqualTo("a hat, a cloak, and a tunic"));
	}

	private static CharacterData CreateCharacters(params CharacterTemplateVariant[] descriptionVariants) => new()
	{
		Templates =
		[
			new CharacterTemplate { Id = CharacterData.FullNameTemplateId, Template = "Hugh" },
			new CharacterTemplate { Id = CharacterData.DescriptionTemplateId, Variants = descriptionVariants },
		],
		Properties = [new CharacterProperty { Id = "mood", Title = "Mood", Type = CharacterPropertyType.ValueList, Values = [new CharacterPropertyOption { Id = "calm", Title = "calm" }] }],
	};
}
