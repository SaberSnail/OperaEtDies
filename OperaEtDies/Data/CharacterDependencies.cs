namespace OperaEtDies.Data;

/// <summary>
/// Works out the order character property values must be chosen in. A property depends on the properties its
/// conditions (and its values' conditions) use, and on the property its string defaults are keyed by, so those
/// must be chosen first.
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
	/// Gets the properties in an order where every property comes after the properties it depends on. Otherwise,
	/// properties keep their order. Dependencies on ids that aren't in the list are ignored. Properties in a cycle
	/// (see <see cref="FindCycle"/>), and properties that depend on them, are left at the end in their original order.
	/// </summary>
	public static IReadOnlyList<CharacterProperty> GetOrder(IReadOnlyList<CharacterProperty> properties)
	{
		var ids = properties.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var remaining = properties.ToList();
		var ordered = new List<CharacterProperty>();
		var placedIds = new HashSet<string>(StringComparer.Ordinal);

		// Repeatedly place the first remaining property whose dependencies are all placed.
		var next = FindReady(remaining, ids, placedIds);
		while (next is not null)
		{
			ordered.Add(next);
			placedIds.Add(next.Id);
			remaining.Remove(next);
			next = FindReady(remaining, ids, placedIds);
		}

		ordered.AddRange(remaining);
		return ordered;
	}

	/// <summary>
	/// Gets the properties that depend on themselves, directly or through other properties, in their original
	/// order. Returns an empty list if there are none.
	/// </summary>
	public static IReadOnlyList<CharacterProperty> FindCycle(IReadOnlyList<CharacterProperty> properties)
	{
		var propertiesById = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in properties)
			propertiesById.TryAdd(property.Id, property);

		IEnumerable<string> GetDependencies(string id) => GetDependencyIds(propertiesById[id]).Where(propertiesById.ContainsKey);

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

	private static CharacterProperty? FindReady(IReadOnlyList<CharacterProperty> remaining, IReadOnlySet<string> ids, IReadOnlySet<string> placedIds) =>
		remaining.FirstOrDefault(x => GetDependencyIds(x).All(id => !ids.Contains(id) || placedIds.Contains(id)));
}
