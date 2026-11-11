namespace OperaEtDies.Data;

/// <summary>
/// Works out the order character property values must be chosen in. A property depends on the properties its
/// conditions (and its values' conditions) use, and on the property its string defaults are keyed by, so those
/// must be chosen first. A property that follows another (see <see cref="CharacterProperty.Follows"/>) is placed right
/// after it, so the other property waits until everything the follower depends on is placed.
/// </summary>
public static class CharacterDependencies
{
	/// <summary>
	/// Gets the ids of the properties a property depends on.
	/// </summary>
	public static IReadOnlyList<string> GetDependencyIds(CharacterProperty property) =>
		property.GetConditionPropertyIds()
			.Concat(new[] { property.Defaults, property.Player?.Defaults, property.Npc?.Defaults }.Select(x => x?.KeyedBy).OfType<string>())
			.Distinct(StringComparer.Ordinal)
			.ToList();

	/// <summary>
	/// Gets the properties in an order where every property comes after the properties it depends on, and right after
	/// the property it follows. Otherwise, properties keep their order. Dependencies and follows on ids that aren't in the
	/// list are ignored. Properties in a cycle (see <see cref="FindCycle"/>), and properties that depend on them, are left
	/// at the end in their original order.
	/// </summary>
	public static IReadOnlyList<CharacterProperty> GetOrder(IReadOnlyList<CharacterProperty> properties)
	{
		var dependencies = GetOrderDependencies(properties);
		var followers = GetFollowers(properties);
		var remaining = properties.ToList();
		var ordered = new List<CharacterProperty>();
		var placedIds = new HashSet<string>(StringComparer.Ordinal);

		// Repeatedly place the first remaining property whose dependencies are all placed, along with its followers.
		var next = remaining.FirstOrDefault(x => dependencies[x].All(placedIds.Contains));
		while (next is not null)
		{
			foreach (var property in GetChain(next, followers).Where(remaining.Contains))
			{
				ordered.Add(property);
				placedIds.Add(property.Id);
				remaining.Remove(property);
			}

			next = remaining.FirstOrDefault(x => dependencies[x].All(placedIds.Contains));
		}

		ordered.AddRange(remaining);
		return ordered;
	}

	/// <summary>
	/// Gets the properties that depend on themselves, directly or through other properties, in their original
	/// order. Following a property counts as depending on it, and the property followed depends on what its followers
	/// depend on. Returns an empty list if there are none.
	/// </summary>
	public static IReadOnlyList<CharacterProperty> FindCycle(IReadOnlyList<CharacterProperty> properties)
	{
		var dependencies = GetOrderDependencies(properties);
		var propertiesById = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in properties)
			propertiesById.TryAdd(property.Id, property);

		IEnumerable<string> GetDependencies(string id) => dependencies[propertiesById[id]];

		bool DependsOnItself(string id)
		{
			var visited = new HashSet<string>(StringComparer.Ordinal);
			var pending = new Stack<string>(GetDependencies(id));
			var found = false;
			while (!found && pending.TryPop(out var dependency))
			{
				if (dependency == id)
				{
					found = true;
				}
				else if (visited.Add(dependency))
				{
					foreach (var nextDependency in GetDependencies(dependency))
						pending.Push(nextDependency);
				}
			}

			return found;
		}

		return properties.Where(x => propertiesById[x.Id] == x && DependsOnItself(x.Id)).ToList();
	}

	/// <summary>
	/// Gets the ids that must be placed before each property: its own dependencies, the property it follows, and the
	/// dependencies of the properties that follow it, apart from the properties placed before them in the chain. Only
	/// ids in the list are included.
	/// </summary>
	private static Dictionary<CharacterProperty, IReadOnlyList<string>> GetOrderDependencies(IReadOnlyList<CharacterProperty> properties)
	{
		var ids = properties.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var followers = GetFollowers(properties);
		var dependencies = new Dictionary<CharacterProperty, IReadOnlyList<string>>();
		foreach (var property in properties)
		{
			var chain = GetChain(property, followers);
			dependencies[property] = chain.SelectMany((member, index) => GetDependencyIds(member).Where(x => !chain.Take(index).Any(earlier => earlier.Id == x)))
				.Concat(property.Follows is { } follows && follows != property.Id ? [follows] : [])
				.Where(ids.Contains)
				.Distinct(StringComparer.Ordinal)
				.ToList();
		}

		return dependencies;
	}

	/// <summary>
	/// Gets the property that follows each property id, using the first one if more than one does.
	/// </summary>
	private static Dictionary<string, CharacterProperty> GetFollowers(IReadOnlyList<CharacterProperty> properties)
	{
		var followers = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in properties.Where(x => x.Follows is not null && x.Follows != x.Id))
			followers.TryAdd(property.Follows!, property);
		return followers;
	}

	/// <summary>
	/// Gets the property, followed by the property that follows it, and so on.
	/// </summary>
	private static IReadOnlyList<CharacterProperty> GetChain(CharacterProperty property, IReadOnlyDictionary<string, CharacterProperty> followers)
	{
		var chain = new List<CharacterProperty> { property };
		var current = property;
		while (followers.TryGetValue(current.Id, out var follower) && !chain.Contains(follower))
		{
			chain.Add(follower);
			current = follower;
		}

		return chain;
	}
}
