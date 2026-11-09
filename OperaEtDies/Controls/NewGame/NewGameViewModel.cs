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

		m_profession = Professions[0];
		m_firstName = m_random.NextWeighted(m_maleFirstNames).Name;
		m_lastName = m_profession.DefaultLastName;
		m_height = gameData.CharacterOptions.GetDefaultHeight();
		m_weight = gameData.CharacterOptions.GetDefaultWeight();
		m_age = MinimumAge;
	}

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartGameRequested;

	public event EventHandler? BackRequested;

	public IReadOnlyList<Profession> Professions { get; }

	public IReadOnlyList<CharacterOption> Heights { get; }

	public IReadOnlyList<CharacterOption> Weights { get; }

	public int MinimumAge { get; }

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
		set => SetPropertyField(value ?? "", ref m_firstName);
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
			var age = Math.Max(value, MinimumAge);
			if (!SetPropertyField(age, ref m_age) && age != value)
			{
				// The value was clamped back to what it already was, so refresh the view to undo the edit.
				RaisePropertyChanged(nameof(Age));
			}
		}
	}

	public void RandomizeFirstName()
	{
		VerifyAccess();

		// Leave out the current name, so the button always appears to do something.
		var otherNames = m_maleFirstNames.Where(x => x.Name != m_firstName).ToList();
		FirstName = m_random.NextWeighted(otherNames.Count != 0 ? otherNames : m_maleFirstNames).Name;
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
