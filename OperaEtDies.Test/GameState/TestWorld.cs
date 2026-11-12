using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// Builds small sets of world objects by hand, for testing them without generating a town.
/// </summary>
internal sealed class TestWorld
{
	public TestWorld(GameData gameData, Random? random = null)
	{
		GameData = gameData;
		Random = random ?? new Random(0);
		Objects = new WorldObjects();
	}

	public GameData GameData { get; }

	public Random Random { get; }

	public WorldObjects Objects { get; }

	public Location AddLocation(LocationTemplate template) => Objects.Create(id => new Location(id, GameData, template));

	/// <summary>
	/// Adds a person who lives alone in a home, and works in a business at the workplace if there is one. Whether they're
	/// working at home is decided as if they had just arrived there.
	/// </summary>
	public Person AddPerson(CharacterTraits traits, Location home, Location? workplace = null)
	{
		var families = GameData.Families;
		var atWorkProperty = GameData.CharacterProperties.Get(families.Properties.AtWork);
		var notAtWork = atWorkProperty.Values!.First(x => x.Id == families.NotAtWork);
		var person = Objects.Create(id => new Person(id, GameData, traits.With(atWorkProperty.Id, notAtWork), FamilyRole.Head, isPlayer: false, home, Random));
		Objects.Create(id => new Family(id, traits.GetString("lastName"), [person], home, WealthLevel.Medium));
		if (workplace is not null && traits.GetValueId(families.Properties.Profession) is { } profession)
			Objects.Create(id => new Business(id, GameData.Professions.Get(profession), person, [], workplace == home ? null : workplace));
		person.PlaceAt(home);
		return person;
	}
}
