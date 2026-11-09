using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Characters;

/// <summary>
/// Picks random traits for a character. Any trait can be predefined by setting its value, and locked so that
/// randomizing leaves it alone. First names follow gender, and last names follow profession, unless they're locked.
/// </summary>
public sealed class CharacterRandomizer
{
	public CharacterRandomizer(GameData gameData, Random random, int minimumAge, int maximumAge)
	{
		if (maximumAge < minimumAge)
			throw new ArgumentOutOfRangeException(nameof(maximumAge), maximumAge, "The maximum age must be at least the minimum age.");

		MinimumAge = minimumAge;
		MaximumAge = maximumAge;

		var firstNames = gameData.FirstNames;
		var options = gameData.CharacterOptions;
		var professions = gameData.Professions.Professions;

		// Traits that depend on other traits are created after them, so their starting values match.
		Gender = new RandomizedValue<Gender>(() => random.Next(2) == 0 ? Characters.Gender.Male : Characters.Gender.Female);
		FirstName = new RandomizedValue<string>(() => random.NextWeighted(Gender.Value == Characters.Gender.Male ? firstNames.Male : firstNames.Female).Name);
		Profession = new RandomizedValue<Profession>(() => random.NextWeighted(professions));
		LastName = new RandomizedValue<string>(() => Profession.Value.DefaultLastName);
		Height = new RandomizedValue<CharacterOption>(() => random.NextWeighted(options.Heights));
		Weight = new RandomizedValue<CharacterOption>(() => random.NextWeighted(options.Weights));

		// Taking the lower of two rolls makes younger ages the most common, with older ages increasingly rare.
		Age = new RandomizedValue<int>(() => minimumAge + Math.Min(random.Next(maximumAge - minimumAge + 1), random.Next(maximumAge - minimumAge + 1)));

		Gender.ValueChanged += (_, _) => FirstName.Randomize();
		Profession.ValueChanged += (_, _) => LastName.Randomize();
	}

	public int MinimumAge { get; }

	public int MaximumAge { get; }

	public RandomizedValue<Gender> Gender { get; }

	/// <summary>
	/// Picked from the first names for the character's gender, and picked again when the gender changes.
	/// </summary>
	public RandomizedValue<string> FirstName { get; }

	public RandomizedValue<Profession> Profession { get; }

	/// <summary>
	/// The profession's default last name, which is updated when the profession changes.
	/// </summary>
	public RandomizedValue<string> LastName { get; }

	public RandomizedValue<CharacterOption> Height { get; }

	public RandomizedValue<CharacterOption> Weight { get; }

	/// <summary>
	/// Picked from the minimum age to the maximum age.
	/// </summary>
	public RandomizedValue<int> Age { get; }

	/// <summary>
	/// Picks new random values for every trait that isn't locked.
	/// </summary>
	public void RandomizeAll()
	{
		Gender.Randomize();
		FirstName.Randomize();
		Profession.Randomize();
		LastName.Randomize();
		Height.Randomize();
		Weight.Randomize();
		Age.Randomize();
	}

	/// <summary>
	/// Gets the current values of every trait.
	/// </summary>
	public CharacterTraits GetTraits() => new(Gender.Value, FirstName.Value, LastName.Value, Profession.Value, Height.Value, Weight.Value, Age.Value);
}
