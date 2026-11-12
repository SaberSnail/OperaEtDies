using System.Collections.ObjectModel;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Creates the households of the town one at a time, keeping track of the businesses and names already taken so
/// that later households fit around them.
/// </summary>
internal sealed class FamilyGenerator
{
	/// <summary>
	/// Creates households for a town. Characters only take up professions that are available in the town, if a way to tell
	/// is given.
	/// </summary>
	public FamilyGenerator(GameData gameData, Random random, Func<Profession, bool>? isProfessionAvailable = null)
	{
		m_gameData = gameData;
		m_random = random;
		m_isProfessionAvailable = isProfessionAvailable ?? (_ => true);
		m_settings = gameData.Families;

		// Families.json is checked against the character properties when the data is loaded.
		var properties = m_settings.Properties;
		var gender = gameData.CharacterProperties.Get(properties.Gender);
		m_male = gender.Values!.First(x => x.Id == m_settings.Male);
		m_female = gender.Values!.First(x => x.Id == m_settings.Female);
		m_age = gameData.CharacterProperties.Get(properties.Age);
		m_ageRange = m_age.GetRange(CharacterRole.Npc);
		m_profession = gameData.CharacterProperties.Get(properties.Profession);
		var workRole = gameData.CharacterProperties.Get(properties.WorkRole);
		m_independent = workRole.Values!.First(x => x.Id == m_settings.Independent);
		m_helper = workRole.Values!.First(x => x.Id == m_settings.Helper);
		var householdRole = gameData.CharacterProperties.Get(properties.HouseholdRole);
		m_head = householdRole.Values!.First(x => x.Id == m_settings.Head);
		m_member = householdRole.Values!.First(x => x.Id == m_settings.Member);
		m_headRelation = gameData.CharacterProperties.Get(properties.HeadRelation);
		var married = gameData.CharacterProperties.Get(properties.Married);
		m_married = married.Values!.First(x => x.Id == m_settings.Married);
		m_unmarried = married.Values!.First(x => x.Id == m_settings.Unmarried);
		m_inheritedProperties = m_settings.InheritedProperties.Select(x => (x, gameData.CharacterProperties.Get(x.Property))).ToList();
		m_otherLastNames = gameData.CharacterProperties.Get(properties.LastName).GetDefaults(CharacterRole.Npc)!.OtherValues!;

		m_households = [];
		m_businesses = [];
		m_businessCounts = new Dictionary<string, int>(StringComparer.Ordinal);
		m_fullNames = new HashSet<string>(StringComparer.Ordinal);
		m_lastNames = new HashSet<string>(StringComparer.Ordinal);
	}

	/// <summary>
	/// The households created so far, starting with the player's.
	/// </summary>
	public IReadOnlyList<Household> Households => m_households;

	public IReadOnlyList<TownBusiness> Businesses => m_businesses;

	/// <summary>
	/// Creates the player's household, with the player at its head. It's created like any other household, except
	/// that a man always has a wife and at least one child if their ages allow it. The player can have any
	/// profession, even if it has no room for more businesses.
	/// </summary>
	public Household AddPlayerHousehold(CharacterTraits player)
	{
		var properties = m_settings.Properties;
		var isFemale = player.GetValueId(properties.Gender) == m_settings.Female;
		var age = player.GetInteger(properties.Age);
		var wifeAge = isFemale ? null : ChooseWifeAge(age, needsChild: true);

		// The game sets the player's roles, like everyone else's.
		var fullName = player.GetFullName(m_gameData.CharacterTemplates);
		var values = player.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
		values[properties.WorkRole] = m_independent;
		values[properties.HouseholdRole] = m_head;
		values[properties.HeadRelation] = GetHeadRelation(m_settings.HeadRelations.Self);
		values[properties.HeadName] = fullName;
		values[properties.Married] = wifeAge is not null ? m_married : m_unmarried;
		player = new CharacterTraits(values);

		var household = new Household(player.GetString(properties.LastName));
		m_lastNames.Add(household.LastName);
		m_fullNames.Add(fullName);

		var head = new TownPerson(player, household, FamilyRole.Head, isPlayer: true);
		household.AddMember(head);
		if (player.Values.GetValueOrDefault(properties.Profession) is CharacterPropertyOption profession && m_gameData.Professions.Find(profession.Id) is not null)
			StartBusiness(head, profession);

		AddRelatives(household, isFemale, age, wifeAge, isPlayer: true);
		return FinishHousehold(household);
	}

	/// <summary>
	/// Creates a household for a non-player character and their family. A woman at the head of a household only has
	/// professions that pay well enough to support it. Whether a man has a wife is decided first, so his profession can
	/// depend on whether he's married.
	/// </summary>
	public Household AddHousehold()
	{
		var isFemale = m_random.NextChance(m_settings.FemaleHeadChance);
		var age = m_random.NextInRange(m_settings.HeadAge);
		var wifeAge = !isFemale && m_random.NextChance(m_settings.WifeChance) ? ChooseWifeAge(age, needsChild: false) : null;
		var character = StartCharacter(isFemale, age, isMarried: wifeAge is not null);

		var profession = ChooseOwnProfession(character, minWealth: isFemale ? m_settings.FemaleHeadMinWealth : null);
		if (profession is not null)
			character[m_profession.Id].Lock(profession);

		var household = new Household(ChooseHeadLastName(character, profession));
		m_lastNames.Add(household.LastName);

		var head = FinishCharacter(character, household, FamilyRole.Head, m_settings.HeadRelations.Self, household.LastName);
		if (profession is not null)
			StartBusiness(head, profession);

		AddRelatives(household, isFemale, age, wifeAge, isPlayer: false);
		return FinishHousehold(household);
	}

	/// <summary>
	/// Adds the head's wife (if the head has one), the children, and a household elder, as the dice allow. A woman at the
	/// head of a household is a widow, and lives with no adult men.
	/// </summary>
	private void AddRelatives(Household household, bool isFemaleHead, int headAge, int? wifeAge, bool isPlayer)
	{
		if (wifeAge is { } wifeAgeValue)
			AddMember(household, FamilyRole.Wife, isFemale: true, wifeAgeValue, household.LastName);

		List<(bool IsFemale, int Age)> parents = [(isFemaleHead, headAge)];
		if (wifeAge is { } motherAge)
			parents.Add((true, motherAge));

		// The oldest children are added first, so they have the first chance to help in the head's business.
		foreach (var age in ChooseChildAges(parents, isPlayer))
			AddMember(household, FamilyRole.Child, isFemaleHead || !m_random.NextChance(m_settings.SonChance), age, household.LastName);

		// The household elder is added last, so they only help in the head's business if there's room left.
		if (m_random.NextChance(m_settings.HouseholdElderChance))
			AddHouseholdElder(household, isFemaleHead, headAge, wifeAge);
	}

	/// <summary>
	/// Adds a parent of the head or the wife, if their age would be old enough to be a household elder. Someone younger
	/// would head their own household instead.
	/// </summary>
	private void AddHouseholdElder(Household household, bool isFemaleHead, int headAge, int? wifeAge)
	{
		// A widow's parent could be her own, or her late husband's, who would share her married name.
		var isWifeParent = wifeAge is not null && m_random.Next(2) == 0;
		var childAge = isWifeParent ? wifeAge!.Value : headAge;
		var sharesLastName = !isWifeParent && (!isFemaleHead || m_random.Next(2) == 0);

		var isFemale = isFemaleHead || m_random.NextChance(m_settings.FemaleHouseholdElderChance);
		var gap = m_random.NextShaped(m_settings.HouseholdElderAgeGap, m_settings.MinParentAge, m_settings.GetMaxParentAge(isFemale), _ => true);
		if (gap is { } ageGap && childAge + ageGap <= m_ageRange.Maximum && childAge + ageGap >= Math.Max(m_ageRange.Minimum, m_settings.GetHouseholdElderMinAge(isFemale)))
		{
			var age = childAge + ageGap;
			// A household elder is widowed.
			var character = StartCharacter(isFemale, age, isMarried: false);
			Inherit(character, [isWifeParent ? household.Members.First(x => x.Role == FamilyRole.Wife) : household.Head]);

			// A man only lives in his child's household if he's too frail to work. A woman helps in a household business if
			// she can. Otherwise she keeps house, or takes up one of the few trades open to household elders.
			var isInfirm = !isFemale || m_random.NextChance(m_settings.InfirmChance.GetValueOrDefault(m_age.GetConditionValueId(age) ?? "", 0));
			var helped = isInfirm ? null : ChooseBusinessToHelp(household, character);
			var trade = isInfirm || helped is not null || m_random.NextChance(m_settings.HouseholdElderHomemakerChance) ? null : ChooseHouseholdElderTrade(character);
			character[m_profession.Id].Lock(trade ?? GetProfessionValue(helped?.Profession.Id ?? (isInfirm ? m_settings.Infirm : m_settings.Homemaker)));

			// A widow who shares her household elder's last name is the elder's daughter-in-law.
			var relations = m_settings.HeadRelations;
			var relation = isFemaleHead ? (sharesLastName ? relations.DaughterInLaw : relations.Daughter) : (isWifeParent ? relations.SonInLaw : relations.Son);
			var person = FinishCharacter(character, household, FamilyRole.HouseholdElder, relation, sharesLastName ? household.LastName : ChooseOtherLastName(), helped);
			if (trade is not null)
			{
				StartBusiness(person, trade);
			}
		}
	}

	private int? ChooseWifeAge(int husbandAge, bool needsChild)
	{
		var range = m_settings.WifeAgeDifference;
		var difference = m_random.NextInRange(range, x =>
			IsAllowedAge(husbandAge + x) && (!needsChild || GetPossibleChildAges([(false, husbandAge), (true, husbandAge + x)]).Count != 0));

		if (difference is null && needsChild)
			Log.Warn($"The player is {husbandAge}, which doesn't allow a wife who can be the mother of an adult child.");

		return husbandAge + difference;
	}

	/// <summary>
	/// Chooses the ages of the children, from oldest to youngest. Their ages tend to be close together.
	/// </summary>
	private IReadOnlyList<int> ChooseChildAges(IReadOnlyList<(bool IsFemale, int Age)> parents, bool needsChild)
	{
		var possibleAges = GetPossibleChildAges(parents);
		var count = m_random.NextInRange(m_settings.ChildCount, x => x <= possibleAges.Count && (!needsChild || x >= 1)) ?? (needsChild ? 1 : 0);
		if (needsChild && possibleAges.Count == 0)
			Log.Warn("The player's family can't have an adult child, because of the parents' ages.");

		var ages = new List<int>();
		if (count > 0 && possibleAges.Count != 0)
		{
			// The first child sets where the rest of the children cluster.
			var chances = m_settings.ChildAge.GetChances(possibleAges.Count);
			var first = possibleAges[m_random.NextWeighted(Enumerable.Range(0, possibleAges.Count).ToList(), x => chances[x])];
			ages.Add(first);

			var remaining = possibleAges.Where(x => x != first).ToList();
			var spread = m_settings.SiblingAgeSpread;
			while (ages.Count < count && remaining.Count != 0)
			{
				// Far away ages get a tiny weight instead of nothing, so there's always something to choose.
				var age = m_random.NextWeighted(remaining, x => Math.Max(Math.Exp(-Math.Pow(x - first, 2) / (2 * spread * spread)), double.Epsilon));
				ages.Add(age);
				remaining.Remove(age);
			}
		}

		return ages.OrderByDescending(x => x).ToList();
	}

	/// <summary>
	/// Gets the ages, from youngest to oldest, that an adult child of all of the given parents could be.
	/// </summary>
	private IReadOnlyList<int> GetPossibleChildAges(IReadOnlyList<(bool IsFemale, int Age)> parents) =>
		Enumerable.Range(m_ageRange.Minimum, m_ageRange.Maximum - m_ageRange.Minimum + 1)
			.Where(age => parents.All(x => x.Age - age >= m_settings.MinParentAge && x.Age - age <= m_settings.GetMaxParentAge(x.IsFemale)))
			.ToList();

	private bool IsAllowedAge(int age) => age >= m_ageRange.Minimum && age <= m_ageRange.Maximum;

	/// <summary>
	/// Adds the wife or a child. They help in a household business if they can and want to. Otherwise they stay at home:
	/// a daughter may keep house, and anyone else works from home. Someone who wanted a business with a place of its own
	/// would have moved out.
	/// </summary>
	private void AddMember(Household household, FamilyRole role, bool isFemale, int age, string lastName)
	{
		// The wife is married to the head. Children living with their parents are unmarried.
		var character = StartCharacter(isFemale, age, isMarried: role == FamilyRole.Wife);
		if (role == FamilyRole.Child)
			Inherit(character, household.Members.Where(x => x.Role is FamilyRole.Head or FamilyRole.Wife).ToList());

		var helped = ChooseBusinessToHelp(household, character);
		var keepsHouse = helped is null && role == FamilyRole.Child && isFemale && m_random.NextChance(m_settings.ChildHomemakerChance);

		var profession = helped is not null ? GetProfessionValue(helped.Profession.Id)
			: keepsHouse ? GetProfessionValue(m_settings.Homemaker)
			: ChooseOwnProfession(character, worksFromHomeOnly: true);
		if (profession is not null)
			character[m_profession.Id].Lock(profession);

		var relations = m_settings.HeadRelations;
		var isFemaleHead = household.Head.Traits.GetValueId(m_settings.Properties.Gender) == m_settings.Female;
		var relation = role == FamilyRole.Wife ? relations.Husband : isFemaleHead ? relations.Mother : relations.Father;
		var person = FinishCharacter(character, household, role, relation, lastName, helped);
		if (helped is null && !keepsHouse && profession is not null)
		{
			StartBusiness(person, profession);
		}
	}

	/// <summary>
	/// Chooses the household business the character helps in: the head's if it has room for them and they're allowed to
	/// help, or else the wife's. Returns null if neither will have them, or if they choose not to help.
	/// </summary>
	private TownBusiness? ChooseBusinessToHelp(Household household, CharacterRandomizer character)
	{
		var business = household.Members
			.Where(x => x.Role is FamilyRole.Head or FamilyRole.Wife && x.IsBusinessOwner)
			.Select(x => x.Business!)
			.FirstOrDefault(x => x.HasRoomForHelper && CanHelp(character, x.Profession));
		return business is not null && m_random.NextChance(m_settings.HelperChance) ? business : null;
	}

	private CharacterPropertyOption GetProfessionValue(string id) => m_profession.Values!.First(x => x.Id == id);

	private CharacterPropertyOption GetHeadRelation(string id) => m_headRelation.Values!.First(x => x.Id == id);

	private bool CanHelp(CharacterRandomizer character, Profession profession) => profession.HelperWhen is { } helperWhen
		? CharacterCondition.IsMet(helperWhen, character.GetValueId)
		: character.GetOptions(m_profession).Any(x => x.Id == profession.Id);

	/// <summary>
	/// Chooses a business the character can have, among those with room for another business, and optionally only those
	/// that work from home or pay at least a given wealth. If none is left after one of these limits, that limit is
	/// ignored. Returns null if the character can't have a business.
	/// </summary>
	private CharacterPropertyOption? ChooseOwnProfession(CharacterRandomizer character, bool worksFromHomeOnly = false, WealthLevel? minWealth = null)
	{
		CharacterPropertyOption? profession = null;
		if (character.HasValue(m_profession))
		{
			// Only values with a profession are businesses. Homemakers and the infirm are only ever household elders.
			IReadOnlyList<CharacterPropertyOption> options = character.GetOptions(m_profession).Where(x => m_gameData.Professions.Find(x.Id) is not null).ToList();
			if (worksFromHomeOnly)
				options = Narrow(options, x => x.WorksFromHome, "No profession the character can have works from home, so any profession is allowed.");
			if (minWealth is { } wealth)
				options = Narrow(options, x => x.GetWealthAmount() >= wealth.GetAmount(), $"No profession the character can have pays at least {wealth.GetDataName()} wealth, so any profession is allowed.");
			options = Narrow(options, m_isProfessionAvailable, "No profession the character can have has a place in this town, so any profession is allowed.");
			options = Narrow(options, x => x.HasRoomForBusiness(m_businessCounts.GetValueOrDefault(x.Id)), "No profession the character can have has room for another business, so the limits are ignored.");

			if (options.Count != 0)
			{
				var chances = m_profession.GetChances(options, m_gameData.FrequencyChances, character.GetValueId);
				profession = options[m_random.NextWeighted(Enumerable.Range(0, options.Count).ToList(), x => chances[x])];
			}
		}

		return profession;
	}

	/// <summary>
	/// Chooses a profession for household elders that the character can have and that has room for another business.
	/// Unlike other choices, the limits are never ignored. Returns null if there's no such profession.
	/// </summary>
	private CharacterPropertyOption? ChooseHouseholdElderTrade(CharacterRandomizer character)
	{
		CharacterPropertyOption? profession = null;
		if (character.HasValue(m_profession))
		{
			var options = character.GetOptions(m_profession)
				.Where(x => m_gameData.Professions.Find(x.Id) is { ForHouseholdElders: true } elderTrade && elderTrade.HasRoomForBusiness(m_businessCounts.GetValueOrDefault(x.Id)))
				.ToList();
			if (options.Count != 0)
			{
				var chances = m_profession.GetChances(options, m_gameData.FrequencyChances, character.GetValueId);
				profession = options[m_random.NextWeighted(Enumerable.Range(0, options.Count).ToList(), x => chances[x])];
			}
		}

		return profession;
	}

	/// <summary>
	/// Gets the options whose professions are allowed, or all of the options (with a warning) if none is.
	/// </summary>
	private IReadOnlyList<CharacterPropertyOption> Narrow(IReadOnlyList<CharacterPropertyOption> options, Func<Profession, bool> isAllowed, string warning)
	{
		var allowed = options.Where(x => isAllowed(m_gameData.Professions.Get(x.Id))).ToList();
		if (allowed.Count == 0 && options.Count != 0)
			Log.Warn(warning);

		return allowed.Count != 0 ? allowed : options;
	}

	/// <summary>
	/// The head's usual last name comes from their character, but a household that would share a last name or a
	/// profession with an earlier household's head gets one from the list of last names instead.
	/// </summary>
	private string ChooseHeadLastName(CharacterRandomizer character, CharacterPropertyOption? profession)
	{
		var usual = character[m_settings.Properties.LastName].Value as string;
		var sharesProfession = profession is not null && m_households.Any(x => x.Head.Traits.GetValueId(m_profession.Id) == profession.Id);
		return string.IsNullOrWhiteSpace(usual) || sharesProfession || m_lastNames.Contains(usual) ? ChooseOtherLastName() : usual;
	}

	/// <summary>
	/// Chooses a last name from the list that's not used yet, if there is one.
	/// </summary>
	private string ChooseOtherLastName()
	{
		var available = m_otherLastNames.Where(x => !m_lastNames.Contains(x.Value)).ToList();
		var lastName = m_gameData.FrequencyChances.Choose(m_random, available.Count != 0 ? available : m_otherLastNames, m_otherLastNames).Value;
		m_lastNames.Add(lastName);
		return lastName;
	}

	/// <summary>
	/// Locks the character's inherited values (see <see cref="FamilySettings.InheritedProperties"/>) to fit their
	/// relatives: the household elder's child, or a child's parents in the household. Relatives without a value the
	/// character is allowed to have are ignored, and a value with no relatives left stays random.
	/// </summary>
	private void Inherit(CharacterRandomizer character, IReadOnlyList<TownPerson> relatives)
	{
		foreach (var (settings, property) in m_inheritedProperties)
		{
			if (character.HasValue(property))
			{
				var allowedIds = character.GetOptions(property).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
				var values = relatives
					.Select(x => x.Traits.Values.GetValueOrDefault(property.Id) as CharacterPropertyOption)
					.OfType<CharacterPropertyOption>()
					.Where(x => allowedIds.Contains(x.Id))
					.ToList();
				if (ChooseInheritedValue(settings, property, values, allowedIds) is { } value)
					character[property.Id].Lock(value);
			}
		}
	}

	/// <summary>
	/// Chooses a value from the relatives' values: the only one, or a blend of two parents' values. Returns null if
	/// there are none, or if both parents' values are unblended, so the value is random.
	/// </summary>
	private CharacterPropertyOption? ChooseInheritedValue(InheritedProperty settings, CharacterProperty property, IReadOnlyList<CharacterPropertyOption> values, IReadOnlySet<string> allowedIds)
	{
		CharacterPropertyOption? value;
		if (values.Count < 2)
		{
			value = values.Count != 0 ? values[0] : null;
		}
		else
		{
			var first = values[0];
			var second = values[1];
			var isFirstUnblended = settings.Unblended.Contains(first.Id, StringComparer.Ordinal);
			var isSecondUnblended = settings.Unblended.Contains(second.Id, StringComparer.Ordinal);
			if (isFirstUnblended && isSecondUnblended)
			{
				value = null;
			}
			else if (isFirstUnblended || isSecondUnblended)
			{
				var unblended = isFirstUnblended ? first : second;
				value = m_random.NextChance(settings.UnblendedChance) ? unblended : (isFirstUnblended ? second : first);
			}
			else if (settings.Blend is { } blend)
			{
				// The parents' values, and the values listed between them that blend and that the character can have.
				var all = property.Values!;
				var ids = all.Select(x => x.Id).ToList();
				var firstIndex = ids.IndexOf(first.Id);
				var secondIndex = ids.IndexOf(second.Id);
				var options = all
					.Skip(Math.Min(firstIndex, secondIndex))
					.Take(Math.Abs(secondIndex - firstIndex) + 1)
					.Where(x => x.Id == first.Id || x.Id == second.Id || (allowedIds.Contains(x.Id) && !settings.Unblended.Contains(x.Id, StringComparer.Ordinal)))
					.ToList();
				var chances = blend.GetChances(options.Count);
				value = options[m_random.NextWeighted(Enumerable.Range(0, options.Count).ToList(), x => chances[x])];
			}
			else
			{
				value = m_random.Next(2) == 0 ? first : second;
			}
		}

		return value;
	}

	/// <summary>
	/// Starts a character with their gender, age, and whether they're married, which are set before anything else so
	/// that conditions on them hold for every choice that follows.
	/// </summary>
	private CharacterRandomizer StartCharacter(bool isFemale, int? age, bool isMarried)
	{
		var properties = m_settings.Properties;
		var character = new CharacterRandomizer(m_gameData, m_random, CharacterRole.Npc);
		character[properties.Gender].Lock(isFemale ? m_female : m_male);
		character[properties.Married].Lock(isMarried ? m_married : m_unmarried);

		var ageValue = character[properties.Age];
		ageValue.Lock(age ?? (ageValue.Value as int? ?? m_ageRange.Minimum));
		return character;
	}

	/// <summary>
	/// Sets the character's last name, roles, and relation to the head of the household, picks the rest of their values,
	/// and adds them to the household, and to the business they help in, if any. Values are picked again a few times if
	/// needed to avoid repeating someone else's full name.
	/// </summary>
	private TownPerson FinishCharacter(CharacterRandomizer character, Household household, FamilyRole role, string headRelationId, string lastName, TownBusiness? helped = null)
	{
		var properties = m_settings.Properties;
		character[properties.LastName].Lock(lastName);
		character[properties.WorkRole].Lock(helped is not null ? m_helper : m_independent);
		character[properties.HouseholdRole].Lock(role == FamilyRole.Head ? m_head : m_member);
		character[properties.HeadRelation].Lock(GetHeadRelation(headRelationId));
		if (role != FamilyRole.Head)
			character[properties.HeadName].Lock(household.Head.Traits.GetString(properties.HeadName));
		character.RandomizeAll();
		for (var attempt = 0; attempt < MaxNameAttempts && m_fullNames.Contains(character.GetTraits().GetFullName(m_gameData.CharacterTemplates)); attempt++)
			character.RandomizeAll();

		// The head's name is their own, which is only known once it's picked.
		var fullName = character.GetTraits().GetFullName(m_gameData.CharacterTemplates);
		if (role == FamilyRole.Head)
			character[properties.HeadName].Lock(fullName);

		var traits = character.GetTraits();
		m_fullNames.Add(fullName);

		var person = new TownPerson(traits, household, role, isPlayer: false);
		household.AddMember(person);
		if (helped is not null)
		{
			helped.AddHelper(person);
			person.Business = helped;
		}

		return person;
	}

	private void StartBusiness(TownPerson owner, CharacterPropertyOption professionValue)
	{
		var profession = m_gameData.Professions.Get(professionValue.Id);
		var business = new TownBusiness(profession, owner);
		owner.Business = business;
		m_businesses.Add(business);
		m_businessCounts[profession.Id] = m_businessCounts.GetValueOrDefault(profession.Id) + 1;
	}

	/// <summary>
	/// Works out the household's wealth: each business owner adds their profession's wealth, each helper adds part of
	/// it, and each homemaker and infirm member adds (or takes away) the amount for them. The total is then adjusted for
	/// the head's age and the household's fortune.
	/// </summary>
	private Household FinishHousehold(Household household)
	{
		var headAge = household.Head.Traits.GetInteger(m_age.Id);
		var ageFactor = m_settings.HeadAgeWealth.GetValueOrDefault(m_age.GetConditionValueId(headAge) ?? "", 100);
		household.Fortune = m_random.NextInRange(m_settings.HouseholdFortune);
		household.Wealth = household.Members.Sum(GetWealth) * ageFactor / 100 * household.Fortune / 100;
		household.WealthLevel = m_settings.GetHouseholdWealthLevel(household.Wealth);
		m_households.Add(household);
		return household;
	}

	/// <summary>
	/// Gets what a member adds to their household's wealth. Each helper in a business adds less than the one before them.
	/// </summary>
	private double GetWealth(TownPerson person)
	{
		var professionId = person.Traits.GetValueId(m_profession.Id);
		return person.Business is { } business
			? business.Profession.GetWealthAmount() * (person.IsBusinessOwner ? 1 : m_settings.HelperWealth * Math.Pow(m_settings.HelperWealthFalloff, business.Helpers.ToList().IndexOf(person)))
			: professionId == m_settings.Homemaker ? m_settings.HomemakerWealth
			: professionId == m_settings.Infirm ? m_settings.InfirmWealth
			: 0;
	}

	private const int MaxNameAttempts = 20;

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(FamilyGenerator));

	private readonly GameData m_gameData;
	private readonly Random m_random;
	private readonly Func<Profession, bool> m_isProfessionAvailable;
	private readonly FamilySettings m_settings;
	private readonly CharacterPropertyOption m_male;
	private readonly CharacterPropertyOption m_female;
	private readonly CharacterProperty m_age;
	private readonly IntegerRange m_ageRange;
	private readonly CharacterProperty m_profession;
	private readonly CharacterPropertyOption m_independent;
	private readonly CharacterPropertyOption m_helper;
	private readonly CharacterPropertyOption m_head;
	private readonly CharacterPropertyOption m_member;
	private readonly CharacterProperty m_headRelation;
	private readonly CharacterPropertyOption m_married;
	private readonly CharacterPropertyOption m_unmarried;
	private readonly IReadOnlyList<(InheritedProperty Settings, CharacterProperty Property)> m_inheritedProperties;
	private readonly IReadOnlyList<WeightedString> m_otherLastNames;
	private readonly Collection<Household> m_households;
	private readonly Collection<TownBusiness> m_businesses;
	private readonly Dictionary<string, int> m_businessCounts;
	private readonly HashSet<string> m_fullNames;
	private readonly HashSet<string> m_lastNames;
}
