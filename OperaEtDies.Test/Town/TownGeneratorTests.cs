using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.Test.Town;

[TestFixture]
public sealed class TownGeneratorTests
{
	[Test]
	public void PlayerHeadsTheirHousehold()
	{
		var gameData = TestData.CreateGameData();
		var player = TestData.CreatePlayerTraits(gameData);

		for (var seed = 0; seed < 50; seed++)
		{
			var town = TownGenerator.Generate(gameData, player, new Random(seed));
			var household = town.PlayerHousehold;

			// The player keeps their chosen traits, and the game sets their roles.
			Assert.That(town.Player.Traits.Values.Where(x => x.Key is not ("workRole" or "householdRole")), Is.EquivalentTo(player.Values.Where(x => x.Key is not ("workRole" or "householdRole"))));
			Assert.That(town.Player.Traits.GetValueId("householdRole"), Is.EqualTo("head"));
			Assert.That(town.Player.IsPlayer, Is.True);
			Assert.That(town.Player.Role, Is.EqualTo(FamilyRole.Head));
			Assert.That(household.LastName, Is.EqualTo("Smith"));
			Assert.That(town.Player.IsBusinessOwner, Is.True);
			Assert.That(town.Player.Business!.Profession.Id, Is.EqualTo("smith"));

			// The player always has a wife and at least one child.
			Assert.That(household.Members.Count(x => x.Role == FamilyRole.Wife), Is.EqualTo(1));
			Assert.That(household.Members.Count(x => x.Role == FamilyRole.Child), Is.GreaterThanOrEqualTo(1));
			Assert.That(town.Households.SelectMany(x => x.Members).Count(x => x.IsPlayer), Is.EqualTo(1));
		}
	}

	[Test]
	public void HouseholdsFollowFamilyRules()
	{
		var gameData = TestData.CreateGameData();
		var settings = gameData.Families;
		var sawWidow = false;
		var sawElder = false;
		var sawInfirm = false;
		var sawHomemaker = false;
		var sawElderTrade = false;
		var sawHelper = false;

		for (var seed = 0; seed < 200; seed++)
		{
			var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
			Assert.That(town.Households.Count - 1, Is.InRange(settings.FamilyCount.Minimum, settings.FamilyCount.Maximum));

			foreach (var household in town.Households)
			{
				var head = household.Head;
				var wife = household.Members.SingleOrDefault(x => x.Role == FamilyRole.Wife);
				var elders = household.Members.Where(x => x.Role == FamilyRole.HouseholdElder).ToList();
				var children = household.Members.Where(x => x.Role == FamilyRole.Child).ToList();

				Assert.That(head.Role, Is.EqualTo(FamilyRole.Head));
				if (!head.IsPlayer)
					Assert.That(GetAge(head), Is.InRange(settings.HeadAge.Minimum, settings.HeadAge.Maximum));
				Assert.That(household.Members.Skip(1).Select(x => x.Role), Is.Not.Contains(FamilyRole.Head));
				Assert.That(elders, Has.Count.LessThanOrEqualTo(1));
				Assert.That(children, Has.Count.LessThanOrEqualTo(settings.ChildCount.Maximum));

				if (IsFemale(head))
				{
					sawWidow = true;
					Assert.That(household.Members.Where(x => !x.IsPlayer).All(IsFemale), Is.True, "A widow's household has no men.");
					Assert.That(wife, Is.Null);
					if (!head.IsPlayer && head.Business is { } business)
						Assert.That(business.Profession.GetWealthAmount(), Is.GreaterThanOrEqualTo(settings.FemaleHeadMinWealth.GetAmount()), "A widow at the head of a household has a trade that pays enough.");
				}

				if (wife is not null)
				{
					Assert.That(IsFemale(wife), Is.True);
					Assert.That(GetAge(wife) - GetAge(head), Is.InRange(settings.WifeAgeDifference.Minimum, settings.WifeAgeDifference.Maximum));
					Assert.That(wife.Traits.GetString("lastName"), Is.EqualTo(household.LastName));
				}

				foreach (var elder in elders)
				{
					sawElder = true;
					var olderThanHead = GetAge(elder) - GetAge(head) >= settings.MinParentAge;
					var olderThanWife = wife is not null && GetAge(elder) - GetAge(wife) >= settings.MinParentAge;
					Assert.That(olderThanHead || olderThanWife, Is.True);
					Assert.That(GetAge(elder), Is.GreaterThanOrEqualTo(settings.GetHouseholdElderMinAge(IsFemale(elder))));

					// Household elders help in a household business, keep house, take up a trade for elders, or are infirm. Men are
					// always infirm.
					var profession = elder.Traits.GetValueId("profession");
					if (!IsFemale(elder))
						Assert.That(profession, Is.EqualTo("infirm"));
					else if (elder.IsBusinessOwner)
						Assert.That(elder.Business!.Profession.ForHouseholdElders, Is.True);
					else if (elder.Business is not null)
						Assert.That(elder.Business.Owner.Role, Is.AnyOf(FamilyRole.Head, FamilyRole.Wife));
					else
						Assert.That(profession, Is.AnyOf("homemaker", "infirm"));
					sawInfirm |= profession == "infirm";
					sawHomemaker |= profession == "homemaker";
					sawElderTrade |= elder.IsBusinessOwner;
				}

				Assert.That(household.Fortune, Is.InRange(settings.HouseholdFortune.Minimum, settings.HouseholdFortune.Maximum));
				Assert.That(household.Wealth, Is.EqualTo(GetExpectedWealth(household, settings)).Within(0.0001));

				// Children are listed from oldest to youngest, and are young enough to be children of both parents.
				Assert.That(children.Select(GetAge), Is.Ordered.Descending);
				Assert.That(children.Select(GetAge), Is.Unique);
				foreach (var child in children)
				{
					Assert.That(child.Traits.GetString("lastName"), Is.EqualTo(household.LastName));
					Assert.That(GetAge(head) - GetAge(child), Is.InRange(settings.MinParentAge, settings.GetMaxParentAge(IsFemale(head))));
					if (wife is not null)
						Assert.That(GetAge(wife) - GetAge(child), Is.InRange(settings.MinParentAge, settings.MaxMotherAge));
				}

				foreach (var person in household.Members.Where(x => !x.IsPlayer))
					Assert.That(GetAge(person), Is.InRange(16, 80));

				sawHelper |= household.Members.Any(x => x.Business is not null && !x.IsBusinessOwner);
			}
		}

		Assert.That(sawWidow, Is.True);
		Assert.That(sawElder, Is.True);
		Assert.That(sawInfirm, Is.True);
		Assert.That(sawHomemaker, Is.True);
		Assert.That(sawElderTrade, Is.True);
		Assert.That(sawHelper, Is.True);
	}

	[Test]
	public void BusinessesFollowProfessionRules()
	{
		var gameData = TestData.CreateGameData();
		var sawWifeHelped = false;
		var sawDaughterKeepingHouse = false;

		for (var seed = 0; seed < 100; seed++)
		{
			var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
			var people = town.Households.SelectMany(x => x.Members).ToList();

			// The player is a smith, and the town only allows one smithy.
			Assert.That(town.Businesses.Count(x => x.Profession.Id == "smith"), Is.EqualTo(1));

			foreach (var person in people)
			{
				// Only helpers have the helper work role, and only heads have the head household role.
				var isHelper = person.Business is not null && !person.IsBusinessOwner;
				Assert.That(person.Traits.GetValueId("workRole"), Is.EqualTo(isHelper ? "helper" : "independent"));
				Assert.That(person.Traits.GetValueId("householdRole"), Is.EqualTo(person.Role == FamilyRole.Head ? "head" : "member"));

				// Everyone works in a business, except household elders and daughters who keep house, and the infirm.
				var profession = person.Traits.GetValueId("profession");
				if (person.Business is null)
					Assert.That((person.Role == FamilyRole.HouseholdElder && profession is "homemaker" or "infirm") || (person.Role == FamilyRole.Child && profession == "homemaker" && IsFemale(person)), Is.True);
				else
					Assert.That(profession, Is.EqualTo(person.Business.Profession.Id));
			}

			foreach (var business in town.Businesses)
			{
				Assert.That(business.Owner.Business, Is.SameAs(business));
				if (business.Profession.Helpers is { } maxHelpers)
					Assert.That(business.Helpers, Has.Count.LessThanOrEqualTo(maxHelpers));
				foreach (var helper in business.Helpers)
				{
					// Helpers only help the head or the wife of their own household.
					Assert.That(helper.Household, Is.SameAs(business.Owner.Household));
					Assert.That(business.Owner.Role, Is.AnyOf(FamilyRole.Head, FamilyRole.Wife));
					Assert.That(helper.Business, Is.SameAs(business));
				}

				if (business.Profession.WorksFromHome)
					Assert.That(business.Location, Is.Null);
				else
					Assert.That(((BusinessTemplate) business.Location!.Template).Profession, Is.EqualTo(business.Profession.Id));

				// Only the head has a business with a place of its own. Anyone who wanted one would have moved out.
				if (business.Owner.Role != FamilyRole.Head)
					Assert.That(business.Profession.WorksFromHome, Is.True);
				sawWifeHelped |= business.Owner.Role == FamilyRole.Wife && business.Helpers.Count != 0;
			}

			sawDaughterKeepingHouse |= people.Any(x => x.Role == FamilyRole.Child && x.Traits.GetValueId("profession") == "homemaker");
		}

		Assert.That(sawWifeHelped, Is.True);
		Assert.That(sawDaughterKeepingHouse, Is.True);
	}

	[Test]
	public void WealthComesFromWork()
	{
		var gameData = TestData.CreateGameData();
		var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(0));

		foreach (var household in town.Households)
		{
			var expected = GetExpectedWealth(household, gameData.Families);
			Assert.That(household.Wealth, Is.EqualTo(expected).Within(0.0001));
			Assert.That(household.WealthLevel, Is.EqualTo(gameData.Families.GetHouseholdWealthLevel(expected)));
		}
	}

	[Test]
	public void LastNamesAreNotRepeated()
	{
		var gameData = TestData.CreateGameData();

		for (var seed = 0; seed < 50; seed++)
		{
			var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));

			Assert.That(town.Households.Select(x => x.LastName), Is.Unique);
		}
	}

	[TestCase(false, LayoutShape.Radial)]
	[TestCase(false, LayoutShape.Linear)]
	[TestCase(false, LayoutShape.Crossroads)]
	[TestCase(false, LayoutShape.Warren)]
	[TestCase(true, null)]
	public void QuartersAreLaidOutOnTheMap(bool useShippedData, LayoutShape? shape)
	{
		// The shipped data chooses among its own layouts.
		var gameData = useShippedData ? GameData.Load(JsonDataLoader.DefaultDataDirectory).Value! : TestData.CreateGameData(shape!.Value);

		for (var seed = 0; seed < 100; seed++)
		{
			var player = useShippedData ? new CharacterRandomizer(gameData, new Random(seed), CharacterRole.Player).GetTraits() : TestData.CreatePlayerTraits(gameData);
			var town = TownGenerator.Generate(gameData, player, new Random(seed));
			var quarters = town.Locations.Where(x => x.Template is QuarterTemplate).ToList();
			if (shape is not null)
				Assert.That(town.Shape, Is.EqualTo(shape));

			// Every unique kind has exactly one quarter, and there are as many landmarks as allowed.
			foreach (var kind in gameData.QuarterKinds.Where(x => x.Unique))
				Assert.That(quarters.Count(x => ((QuarterTemplate) x.Template).Kind == kind.Id), Is.EqualTo(1), kind.Id);
			Assert.That(quarters.Count(x => ((QuarterTemplate) x.Template).IsLandmark), Is.InRange(gameData.LandmarkCount.Minimum, gameData.LandmarkCount.Maximum));

			// Quarters are rectangles of their size that don't overlap. Houses and businesses aren't on the map.
			Assert.That(town.Locations.Where(x => x.Template is not QuarterTemplate).Select(x => x.Bounds), Has.All.Null);
			foreach (var quarter in quarters)
			{
				var size = ((QuarterTemplate) quarter.Template).Size;
				var bounds = quarter.Bounds!.Value;
				Assert.That(new[] { bounds.Width, bounds.Height }, Is.EquivalentTo(new[] { size.Length, size.Width }));
				Assert.That(quarters.Where(x => x != quarter).Select(x => x.Bounds!.Value.Overlaps(bounds)), Has.None.True);
			}

			foreach (var quarter in quarters)
			{
				var paths = quarter.Exits.Where(x => x.Location.Template is QuarterTemplate).ToList();

				// Quarters only connect to quarters they touch, by a named path that leaves by the side it points to, give
				// or take one step. Each path leads back the opposite way, and no quarter has two exits the same way.
				Assert.That(paths.Select(x => x.Direction), Is.Unique);
				foreach (var exit in paths)
				{
					var edge = quarter.Bounds!.Value.GetSharedEdge(exit.Location.Bounds!.Value, gap: exit.IsCrossing ? 1 : 0);
					Assert.That(edge, Is.Not.Null);
					Assert.That(exit.Direction, Is.AnyOf(edge!.Value.Side, edge.Value.Side.Turn(-1), edge.Value.Side.Turn(1)));
					Assert.That(exit.Path, Is.Not.Empty);
					var back = exit.Location.Exits.Single(x => x.Location == quarter);
					Assert.That(back.Direction, Is.EqualTo(exit.Direction!.Value.Opposite()));
					Assert.That(back.Path, Is.EqualTo(exit.Path));
				}

				// Houses and businesses are reached from their quarter without a direction.
				Assert.That(quarter.Exits.Where(x => x.Location.Template is not QuarterTemplate).Select(x => x.Direction), Has.All.Null);
			}
		}
	}

	[Test]
	public void FeaturesAreOnTheMapAndDescribed()
	{
		var gameData = TestData.CreateGameData(withFeatures: true);
		var sawCrossing = false;
		var riverbanks = 0;
		var riverbanksByRiver = 0;

		for (var seed = 0; seed < 100; seed++)
		{
			var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
			var quarters = town.Locations.Where(x => x.Template is QuarterTemplate).ToList();
			var river = town.Features.Single(x => x.Feature.Id == "river").Bounds;

			// No quarter sits on the river or an area.
			foreach (var feature in town.Features)
				Assert.That(quarters.Select(x => x.Bounds!.Value.Overlaps(feature.Bounds)), Has.None.True, feature.Feature.Id);

			foreach (var quarter in quarters)
			{
				var bounds = quarter.Bounds!.Value;

				// A path across the river is a bridge between quarters facing each other across it. Any other path is
				// between quarters that touch.
				foreach (var exit in quarter.Exits.Where(x => x.Direction is not null))
				{
					var other = exit.Location.Bounds!.Value;
					if (exit.IsCrossing)
					{
						Assert.That(bounds.GetSharedEdge(other), Is.Null);
						Assert.That(bounds.GetSharedEdge(other, gap: 1), Is.Not.Null);
						Assert.That(exit.Path, Is.EqualTo("bridge"));
						sawCrossing = true;
					}
					else
					{
						Assert.That(bounds.GetSharedEdge(other), Is.Not.Null);
					}
				}

				// A quarter mentions each feature it borders, in the direction of the side it borders.
				foreach (var surrounding in quarter.Surroundings)
				{
					var featureBounds = town.Features.Single(x => x.Feature == surrounding.Feature).Bounds;
					var side = surrounding.Feature.Type == MapFeatureType.River
						? (bounds.GetSharedEdge(featureBounds) ?? bounds.GetSharedEdge(new MapRect(int.MinValue / 2, river.Y, int.MaxValue, river.Height)) ?? bounds.GetSharedEdge(new MapRect(river.X, int.MinValue / 2, river.Width, int.MaxValue)))!.Value.Side
						: bounds.GetSharedEdge(featureBounds)!.Value.Side;
					Assert.That(surrounding.Direction, Is.AnyOf(side, side.Turn(-1), side.Turn(1)));
				}

				var template = (QuarterTemplate) quarter.Template;
				if (template.Id == "clearing")
					Assert.That(quarter.Surroundings.Single(x => x.Feature.Id == "woods").Text, Is.EqualTo($"The trees stand {quarter.Surroundings.Single(x => x.Feature.Id == "woods").Direction.GetTitle().ToLowerInvariant()}."));
				if (template.Kind == "riverbank")
				{
					riverbanks++;
					if (quarter.Surroundings.Any(x => x.Feature.Id == "river"))
						riverbanksByRiver++;
				}
			}
		}

		// A quarter that must border the river does, unless nothing could reach it, which is rare.
		Assert.That(sawCrossing, Is.True);
		Assert.That(riverbanksByRiver, Is.GreaterThanOrEqualTo(riverbanks * 0.9));
	}

	[Test]
	public void QuartersThatNeedAMissingFeatureAreLeftOut()
	{
		var gameData = TestData.CreateGameData();

		for (var seed = 0; seed < 50; seed++)
		{
			var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));

			Assert.That(town.Features, Is.Empty);
			Assert.That(town.Locations.Select(x => x.Template.Id), Does.Not.Contain("bank").And.Not.Contain("clearing"));
			Assert.That(town.Locations.SelectMany(x => x.Exits).Select(x => x.IsCrossing), Has.None.True);
		}
	}

	[Test]
	public void ProfessionsNeedAPlaceTheTownCanHave()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var fisher = gameData.Professions.Get("profession_fisher");
		var smith = gameData.Professions.Get("profession_smith");
		var spinster = gameData.Professions.Get("profession_spinster");

		Assert.That(TownGenerator.IsProfessionAvailable(gameData, fisher, new HashSet<string>()), Is.False);
		Assert.That(TownGenerator.IsProfessionAvailable(gameData, fisher, new HashSet<string> { "river" }), Is.True);
		Assert.That(TownGenerator.IsProfessionAvailable(gameData, smith, new HashSet<string>()), Is.True);
		Assert.That(TownGenerator.IsProfessionAvailable(gameData, spinster, new HashSet<string>()), Is.True);

		// Without a river, nobody fishes, unless the player does, in which case the town gets a river.
		for (var seed = 0; seed < 200; seed++)
		{
			var player = new CharacterRandomizer(gameData, new Random(seed), CharacterRole.Player).GetTraits();
			var town = TownGenerator.Generate(gameData, player, new Random(seed));
			if (!town.Features.Any(x => x.Feature.Id == "river"))
				Assert.That(town.Businesses.Select(x => x.Profession.Id), Has.None.EqualTo("profession_fisher"));
		}
	}

	[Test]
	public void TownIsLaidOutAroundHouseholds()
	{
		var gameData = TestData.CreateGameData();

		for (var seed = 0; seed < 100; seed++)
		{
			var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
			var quarters = town.Locations.Where(x => x.Template is QuarterTemplate).ToList();

			Assert.That(town.Locations.Select(x => x.Id), Is.Unique);
			Assert.That(town.Households.Select(x => x.House), Is.Unique);

			foreach (var household in town.Households)
			{
				var house = (HouseTemplate) household.House!.Template;
				var quarter = household.House.Connections.Single();
				Assert.That(house.Wealth, Is.EqualTo(household.WealthLevel));
				Assert.That(((QuarterTemplate) quarter.Template).Wealth, Is.EqualTo(household.WealthLevel));
			}

			foreach (var quarter in quarters)
			{
				var template = (QuarterTemplate) quarter.Template;
				Assert.That(quarter.Connections.Count(x => x.Template is HouseTemplate), Is.LessThanOrEqualTo(template.MaxHouses));
				Assert.That(quarter.Connections.Count(x => x.Template is BusinessTemplate), Is.LessThanOrEqualTo(template.Businesses));
			}

			foreach (var location in town.Locations)
			{
				foreach (var other in location.Connections)
					Assert.That(other.Connections, Does.Contain(location));
			}

			// Every place can be reached from the player's house.
			var reached = new HashSet<TownLocation> { town.PlayerHousehold.House! };
			var pending = new Queue<TownLocation>(reached);
			while (pending.TryDequeue(out var location))
			{
				foreach (var other in location.Connections.Where(reached.Add))
					pending.Enqueue(other);
			}

			Assert.That(reached, Is.EquivalentTo(town.Locations));
		}
	}

	[Test]
	public void ShippedDataCreatesTown()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var settings = gameData.Families;

		for (var seed = 0; seed < 100; seed++)
		{
			var player = new CharacterRandomizer(gameData, new Random(seed), CharacterRole.Player).GetTraits();
			var town = TownGenerator.Generate(gameData, player, new Random(seed));

			Assert.That(town.Households.Count - 1, Is.InRange(settings.FamilyCount.Minimum, settings.FamilyCount.Maximum));
			Assert.That(town.Households.Select(x => x.House), Has.All.Not.Null);
			Assert.That(town.Businesses.Where(x => !x.Profession.WorksFromHome).Select(x => x.Location), Has.All.Not.Null);
			Assert.That(town.Households.Select(x => x.LastName), Is.Unique);
			Assert.That(town.Households.SelectMany(x => x.Members).Select(x => x.Traits.GetFullName(gameData.CharacterTemplates)), Is.Unique);
			foreach (var business in town.Businesses.Where(x => x.Profession.Helpers is not null))
				Assert.That(business.Helpers, Has.Count.LessThanOrEqualTo(business.Profession.Helpers!.Value));

			// Heads are described as heading their household, and no one else is.
			foreach (var person in town.Households.SelectMany(x => x.Members))
				Assert.That(person.Traits.GetDescription(gameData.CharacterTemplates).Contains($" is the head of the {person.Household.LastName} household.", StringComparison.Ordinal), Is.EqualTo(person.Role == FamilyRole.Head));

			// Helpers are described as helping, not as running the trade themselves.
			foreach (var helper in town.Businesses.SelectMany(x => x.Helpers))
				Assert.That(helper.Traits.GetDescription(gameData.CharacterTemplates), Does.Contain(" helps ").Or.Contain(" assists "));

			// Only the head has a business with a place of its own. Everyone else who runs a business works from home.
			foreach (var business in town.Businesses.Where(x => x.Owner.Role != FamilyRole.Head))
				Assert.That(business.Profession.WorksFromHome, Is.True, business.Profession.Id);
		}
	}

	/// <summary>
	/// Works out a household's wealth the way Families.json describes it: business owners add their trade's wealth, each
	/// helper adds less than the one before, homemakers and the infirm add their own amounts, and the total is adjusted
	/// for the head's age and the household's fortune.
	/// </summary>
	private static double GetExpectedWealth(Household household, FamilySettings settings)
	{
		var total = household.Members.Sum(x => x.Business is { } business
			? business.Profession.GetWealthAmount() * (x.IsBusinessOwner ? 1 : settings.HelperWealth * Math.Pow(settings.HelperWealthFalloff, business.Helpers.ToList().IndexOf(x)))
			: x.Traits.GetValueId("profession") == settings.Homemaker ? settings.HomemakerWealth : settings.InfirmWealth);
		var headAge = GetAge(household.Head);
		var ageGroup = headAge < 30 ? "young" : headAge < 50 ? "adult" : headAge < 70 ? "old" : "veryOld";
		return total * settings.HeadAgeWealth.GetValueOrDefault(ageGroup, 100) / 100 * household.Fortune / 100;
	}

	private static bool IsFemale(TownPerson person) => person.Traits.GetValueId("gender") == "female";

	private static int GetAge(TownPerson person) => person.Traits.GetInteger("age");
}
