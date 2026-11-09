using System.Globalization;
using GoldenAnvil.Utility;
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
		m_maleFirstNames = gameData.FirstNames.Male;
		m_random = random;

		Professions = gameData.Professions.Professions;
		Heights = gameData.CharacterOptions.Heights;
		Weights = gameData.CharacterOptions.Weights;
		MinimumAge = gameData.CharacterOptions.PlayerMinimumAge;
		MaximumAge = gameData.CharacterOptions.PlayerMaximumAge;

		m_profession = Professions[0];
		m_firstName = m_random.NextWeighted(m_maleFirstNames).Name;
		m_lastName = m_profession.DefaultLastName;
		// Placeholders until the randomize calls below pick the starting values.
		m_height = Heights[0];
		m_weight = Weights[0];
		m_age = MinimumAge;

		RandomizeHeight();
		RandomizeWeight();
		RandomizeAge();
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
		get => VerifyAccess(m_profession);
		set
		{
			// The combo box can push null if its items change.
			if (value is null || !SetPropertyField(value, ref m_profession))
				return;

			// The last name follows the profession until the player picks their own.
			if (!m_isLastNameCustomized)
				SetPropertyField(value.DefaultLastName, ref m_lastName, nameof(LastName));
		}
	}

	public string FirstName
	{
		get => VerifyAccess(m_firstName);
		set
		{
			// A blank first name is replaced with a random one.
			if (string.IsNullOrWhiteSpace(value))
			{
				RandomizeFirstName();

				// The random name may be the one already set, so refresh the view to replace its blank text.
				RaisePropertyChanged(nameof(FirstName));
				return;
			}

			SetPropertyField(value, ref m_firstName);
		}
	}

	public string LastName
	{
		get => VerifyAccess(m_lastName);
		set
		{
			// A blank last name goes back to the profession's default.
			var lastName = string.IsNullOrWhiteSpace(value) ? m_profession.DefaultLastName : value;

			// Typing the profession's default also goes back to following the profession.
			m_isLastNameCustomized = lastName != m_profession.DefaultLastName;

			if (!SetPropertyField(lastName, ref m_lastName) && lastName != value)
			{
				// The value was replaced with what it already was, so refresh the view to undo the edit.
				RaisePropertyChanged(nameof(LastName));
			}
		}
	}

	public CharacterOption Height
	{
		get => VerifyAccess(m_height);
		set
		{
			if (value is not null)
				SetPropertyField(value, ref m_height);
		}
	}

	public CharacterOption Weight
	{
		get => VerifyAccess(m_weight);
		set
		{
			if (value is not null)
				SetPropertyField(value, ref m_weight);
		}
	}

	public int Age
	{
		get => VerifyAccess(m_age);
		set
		{
			var age = Math.Clamp(value, MinimumAge, MaximumAge);
			if (SetPropertyField(age, ref m_age))
			{
				RaisePropertyChanged(nameof(AgeText));
			}
			else if (age != value)
			{
				// The value was clamped back to what it already was, so refresh the view to undo the edit.
				RaisePropertyChanged(nameof(Age));
				RaisePropertyChanged(nameof(AgeText));
			}
		}
	}

	/// <summary>
	/// The age as text, for editing. Blank text is replaced with a random age, and text that isn't a number is undone.
	/// </summary>
	public string AgeText
	{
		get => VerifyAccess(m_age).ToString(CultureInfo.CurrentCulture);
		set
		{
			if (string.IsNullOrWhiteSpace(value))
				RandomizeAge();
			else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var age))
				Age = age;

			// The view's text may not match the age (blank, not a number, or a random age that didn't change), so always refresh it.
			RaisePropertyChanged(nameof(AgeText));
		}
	}

	public void RandomizeProfession()
	{
		VerifyAccess();
		Profession = m_random.NextWeighted(Professions);
	}

	public void RandomizeFirstName()
	{
		VerifyAccess();
		FirstName = m_random.NextWeighted(m_maleFirstNames).Name;
	}

	public void RandomizeHeight()
	{
		VerifyAccess();
		Height = m_random.NextWeighted(Heights);
	}

	public void RandomizeWeight()
	{
		VerifyAccess();
		Weight = m_random.NextWeighted(Weights);
	}

	public void RandomizeAge()
	{
		VerifyAccess();
		Age = NextAge();
	}

	public void RandomizeAll()
	{
		VerifyAccess();
		RandomizeProfession();
		RandomizeFirstName();
		RandomizeHeight();
		RandomizeWeight();
		RandomizeAge();
	}

	public bool CanStartGame() => !string.IsNullOrWhiteSpace(m_firstName) && !string.IsNullOrWhiteSpace(m_lastName);

	public void StartGame()
	{
		VerifyAccess();
		if (!CanStartGame())
			return;

		var settings = new NewGameSettings(m_profession, m_firstName.Trim(), m_lastName.Trim(), m_height, m_weight, m_age);
		StartGameRequested.Raise(this, new GenericEventArgs<NewGameSettings>(settings));
	}

	public void Back()
	{
		VerifyAccess();
		BackRequested.Raise(this);
	}

	/// <summary>
	/// Picks a random age from the minimum to the maximum. Taking the lower of two rolls makes younger ages
	/// the most common, with older ages increasingly rare.
	/// </summary>
	private int NextAge() => MinimumAge + Math.Min(m_random.Next(MaximumAge - MinimumAge + 1), m_random.Next(MaximumAge - MinimumAge + 1));

	private readonly IReadOnlyList<WeightedName> m_maleFirstNames;
	private readonly Random m_random;
	private Profession m_profession;
	private string m_firstName;
	private string m_lastName;
	private bool m_isLastNameCustomized;
	private CharacterOption m_height;
	private CharacterOption m_weight;
	private int m_age;
}
