using System.Globalization;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Lets the player configure their character before starting a new game.
/// </summary>
public sealed class NewGameViewModel : ViewModelBase
{
	public NewGameViewModel(GameData gameData, Random random)
	{
		Professions = gameData.Professions.Professions;
		Heights = gameData.CharacterOptions.Heights;
		Weights = gameData.CharacterOptions.Weights;
		MinimumAge = gameData.CharacterOptions.PlayerMinimumAge;
		MaximumAge = gameData.CharacterOptions.PlayerMaximumAge;

		m_character = new CharacterRandomizer(gameData, random, MinimumAge, MaximumAge);

		// The player is always a man.
		m_character.Gender.Lock(Gender.Male);

		// The profession starts as the first one, but can still be randomized.
		m_character.Profession.Value = Professions[0];
	}

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartGameRequested;

	public event EventHandler? BackRequested;

	public IReadOnlyList<Profession> Professions { get; }

	public IReadOnlyList<CharacterOption> Heights { get; }

	public IReadOnlyList<CharacterOption> Weights { get; }

	public int MinimumAge { get; }

	public int MaximumAge { get; }

	public Profession Profession
	{
		get => VerifyAccess(m_character.Profession.Value);
		set
		{
			// The combo box can push null if its items change. The last name follows the profession unless the player picked their own.
			if (value is not null)
				SetTrait(m_character.Profession, value, nameof(Profession), nameof(LastName));
		}
	}

	public string FirstName
	{
		get => VerifyAccess(m_character.FirstName.Value);
		set
		{
			// A blank first name is replaced with a random one. Randomizing always notifies, which also refreshes the view's
			// blank text when the random name is the one already set.
			if (string.IsNullOrWhiteSpace(value))
				RandomizeFirstName();
			else
				SetTrait(m_character.FirstName, value, nameof(FirstName));
		}
	}

	public string LastName
	{
		get => VerifyAccess(m_character.LastName.Value);
		set
		{
			// A blank last name goes back to the profession's default.
			var defaultLastName = m_character.Profession.Value.DefaultLastName;
			var lastName = string.IsNullOrWhiteSpace(value) ? defaultLastName : value;

			// A custom last name is locked so it's kept when the profession changes. Typing the profession's default
			// unlocks it, so it goes back to following the profession.
			var lastNameValue = m_character.LastName;
			lastNameValue.IsLocked = lastName != defaultLastName;

			// If the value was replaced with what it already was, the view still needs a refresh to undo the edit.
			if (lastNameValue.Value == lastName && lastName == value)
				return;

			using (ScopedPropertyChange(nameof(LastName)))
				lastNameValue.Value = lastName;
		}
	}

	public CharacterOption Height
	{
		get => VerifyAccess(m_character.Height.Value);
		set
		{
			if (value is not null)
				SetTrait(m_character.Height, value, nameof(Height));
		}
	}

	public CharacterOption Weight
	{
		get => VerifyAccess(m_character.Weight.Value);
		set
		{
			if (value is not null)
				SetTrait(m_character.Weight, value, nameof(Weight));
		}
	}

	public int Age
	{
		get => VerifyAccess(m_character.Age.Value);
		set
		{
			// If the value was clamped back to what it already was, the view still needs a refresh to undo the edit.
			var age = Math.Clamp(value, MinimumAge, MaximumAge);
			if (m_character.Age.Value == age && age == value)
				return;

			using (ScopedPropertyChange(nameof(Age), nameof(AgeText)))
				m_character.Age.Value = age;
		}
	}

	/// <summary>
	/// The age as text, for editing. Blank text is replaced with a random age, and text that isn't a number is undone.
	/// </summary>
	public string AgeText
	{
		get => VerifyAccess(m_character.Age.Value).ToString(CultureInfo.CurrentCulture);
		set
		{
			// The view's text may not match the age (blank, not a number, or a random age that didn't change), so always refresh it.
			using (ScopedPropertyChange(nameof(Age), nameof(AgeText)))
			{
				if (string.IsNullOrWhiteSpace(value))
					m_character.Age.Randomize();
				else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var age))
					m_character.Age.Value = Math.Clamp(age, MinimumAge, MaximumAge);
			}
		}
	}

	public void RandomizeProfession()
	{
		using (ScopedPropertyChange(nameof(Profession), nameof(LastName)))
			m_character.Profession.Randomize();
	}

	public void RandomizeFirstName()
	{
		using (ScopedPropertyChange(nameof(FirstName)))
			m_character.FirstName.Randomize();
	}

	public void RandomizeHeight()
	{
		using (ScopedPropertyChange(nameof(Height)))
			m_character.Height.Randomize();
	}

	public void RandomizeWeight()
	{
		using (ScopedPropertyChange(nameof(Weight)))
			m_character.Weight.Randomize();
	}

	public void RandomizeAge()
	{
		using (ScopedPropertyChange(nameof(Age), nameof(AgeText)))
			m_character.Age.Randomize();
	}

	/// <summary>
	/// Randomizes everything except a custom last name.
	/// </summary>
	public void RandomizeAll()
	{
		using (ScopedPropertyChange(nameof(Profession), nameof(FirstName), nameof(LastName), nameof(Height), nameof(Weight), nameof(Age), nameof(AgeText)))
			m_character.RandomizeAll();
	}

	public bool CanStartGame() => !string.IsNullOrWhiteSpace(m_character.FirstName.Value) && !string.IsNullOrWhiteSpace(m_character.LastName.Value);

	public void StartGame()
	{
		VerifyAccess();
		if (!CanStartGame())
			return;

		var traits = m_character.GetTraits();
		var player = traits with { FirstName = traits.FirstName.Trim(), LastName = traits.LastName.Trim() };
		StartGameRequested.Raise(this, new GenericEventArgs<NewGameSettings>(new NewGameSettings(player)));
	}

	public void Back()
	{
		VerifyAccess();
		BackRequested.Raise(this);
	}

	/// <summary>
	/// Sets a trait, raising change notifications for the given properties if the value changes.
	/// </summary>
	private void SetTrait<T>(RandomizedValue<T> trait, T value, params string[] propertyNames)
	{
		if (EqualityComparer<T>.Default.Equals(trait.Value, value))
			return;

		using (ScopedPropertyChange(propertyNames))
			trait.Value = value;
	}

	private readonly CharacterRandomizer m_character;
}
