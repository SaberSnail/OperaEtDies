using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// How comfortable people are with how covered they are, and so how covered their modesty rule says they must be (see
/// <see cref="ModestySettings"/>). Comfort starts from the company someone is in: the least comfortable of the people who
/// can see them, with someone asleep counting part of the way toward no one being there. With no one there, it's how
/// likely someone is to come by. The place, the step they're on, and their boldness add to it.
/// </summary>
internal static class ModestyComfort
{
	/// <summary>
	/// Gets how comfortable someone is with how covered they are where they are now, with the people there (counting those
	/// asleep as asleep) and the step they're on (see <see cref="GetCurrentStep"/>).
	/// </summary>
	public static double Get(GameData gameData, Person person, TimePoint time) =>
		Get(gameData, person, person.Location, person.Location.People.Where(x => x != person).Select(x => (x, x.IsAsleep)), GetCurrentStep(person), time);

	/// <summary>
	/// Gets the step someone is on, for their comfort: the step of their current sequence, or while they take part in
	/// someone else's steps, such as an appointment's client, the step the one they follow is on.
	/// </summary>
	public static Step? GetCurrentStep(Person person) =>
		person.Current is { Role: not SequenceRole.Steps, Leader: { } leader } ? leader.Current?.CurrentStep : person.Current?.CurrentStep;

	/// <summary>
	/// Gets how comfortable someone would be with how covered they are at a location, seen by the given people (and whether
	/// each is asleep), on a step, if there is one, at the given time.
	/// </summary>
	public static double Get(GameData gameData, Person person, Location location, IEnumerable<(Person Person, bool IsAsleep)> company, Step? step, TimePoint time)
	{
		var settings = gameData.ModestySettings;
		var alone = GetAloneComfort(gameData, location, time);
		var genderId = gameData.Families.Roles.Properties.Gender;
		var gender = person.Traits.GetValueId(genderId);
		var others = company.Where(x => x.Person != person).ToList();
		var comfort = (double) alone;
		if (others.Count != 0)
		{
			comfort = others.Min(x => GetPersonComfort(gameData, person, x.Person, x.IsAsleep, alone));
			if (others.All(x => x.Person.Traits.GetValueId(genderId) == gender))
				comfort += settings.SameGender;
		}

		comfort += GetPlaceComfort(gameData, location) + (step?.ModestyComfort ?? 0) + settings.GetBoldnessComfort(person.Traits);
		return Math.Clamp(comfort, 0, ModestySettings.MaxComfort);
	}

	/// <summary>
	/// Gets the comfort of being at a location with no one there: how likely someone is to come by, from the location's
	/// traffic (or its quarter kind's, or the modesty settings' for its kind of location), and the part of the day.
	/// </summary>
	public static int GetAloneComfort(GameData gameData, Location location, TimePoint time)
	{
		var settings = gameData.ModestySettings;
		var traffic = location.Template.Traffic ?? location.Template switch
		{
			QuarterTemplate quarter => gameData.QuarterKinds.Find(quarter.Kind)?.Traffic ?? settings.Traffic.Quarter,
			HouseTemplate => settings.Traffic.House,
			_ => settings.Traffic.Business,
		};
		return settings.GetTrafficComfort(traffic, gameData.Calendar.GetDayPart(time).TrafficSteps);
	}

	/// <summary>
	/// Gets how much more comfortable people are at a location: its own comfort, or for a quarter, its kind's.
	/// </summary>
	public static int GetPlaceComfort(GameData gameData, Location location) =>
		location.Template.ModestyComfort ?? (location.Template is QuarterTemplate quarter ? gameData.QuarterKinds.Find(quarter.Kind)?.ModestyComfort ?? 0 : 0);

	/// <summary>
	/// Gets the comfort of being seen by someone: by how close they are, or for someone asleep, part of the way from no one
	/// being there to that.
	/// </summary>
	private static double GetPersonComfort(GameData gameData, Person person, Person other, bool isAsleep, int alone)
	{
		var settings = gameData.ModestySettings;
		var company = settings.Company;
		var awake = IsSpouse(person, other) ? company.Spouse :
			other.Family == person.Family ? company.Household :
			person.Relationships.TryGetValue(other, out var relationship) && gameData.Relationships.IsFriend(relationship.Score) ? company.Friend :
			company.Anyone;
		return isAsleep ? alone + ((awake - alone) * settings.Asleep / 100.0) : awake;
	}

	/// <summary>
	/// Whether two people are married to each other: the head of a household and the wife.
	/// </summary>
	private static bool IsSpouse(Person person, Person other) =>
		person.Family == other.Family && person.Role is FamilyRole.Head or FamilyRole.Wife && other.Role is FamilyRole.Head or FamilyRole.Wife && person.Role != other.Role;
}
