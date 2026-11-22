using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Data;

/// <summary>
/// Marks a property of the data as only allowed when another property (such as a step's kind) has one of the given values.
/// Without values, the other property only has to be set, or with <see cref="Unset"/>, not set. The other property is on
/// the same object, or with <see cref="OnParent"/>, on the object that holds this one. The game's validation and the mod
/// editor both read it, so they agree.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedForAttribute(string property, params object[] values) : Attribute
{
	/// <summary>
	/// The name of the property it depends on, as the class names it.
	/// </summary>
	public string Property { get; } = property;

	public IReadOnlyList<object> Values { get; } = values;

	/// <summary>
	/// Whether the other property must not be set, rather than set. Only used without values.
	/// </summary>
	public bool Unset { get; init; }

	/// <summary>
	/// Whether the other property is on the object that holds this one, rather than on the same object.
	/// </summary>
	public bool OnParent { get; init; }

	internal DataRule CreateRule(Type type) => DataRules.CreateRule(type, Property, Values, Unset, OnParent);
}

/// <summary>
/// Marks a property of the data as required when another property (such as a step's kind) has one of the given values,
/// with the same choices as <see cref="AllowedForAttribute"/>. Validation can require it in other cases too.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredForAttribute(string property, params object[] values) : Attribute
{
	/// <summary>
	/// The name of the property it depends on, as the class names it.
	/// </summary>
	public string Property { get; } = property;

	public IReadOnlyList<object> Values { get; } = values;

	/// <summary>
	/// Whether the other property must not be set, rather than set. Only used without values.
	/// </summary>
	public bool Unset { get; init; }

	/// <summary>
	/// Whether the other property is on the object that holds this one, rather than on the same object.
	/// </summary>
	public bool OnParent { get; init; }

	internal DataRule CreateRule(Type type) => DataRules.CreateRule(type, Property, Values, Unset, OnParent);
}

/// <summary>
/// A rule that a property of the data depends on another property: the other property's name and the values it must have,
/// as written in the data files. Without values, the other property must be set, or with <see cref="IsUnset"/>, not set.
/// With <see cref="IsOnParent"/>, the other property is on the object that holds this one.
/// </summary>
public sealed record DataRule(string Property, IReadOnlyList<string> Values, bool IsUnset = false, bool IsOnParent = false)
{
	/// <summary>
	/// Whether the rule is met by the other property, given whether it's set and its value as written in the data.
	/// </summary>
	public bool IsMetBy(bool isSet, string? value) =>
		Values.Count == 0 ? isSet != IsUnset : value is not null && Values.Contains(value, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Works out which of an object's properties are allowed or required, from <see cref="AllowedForAttribute"/> and
/// <see cref="RequiredForAttribute"/>. Rules on the object that holds this one need it as the parent.
/// </summary>
public static class DataRules
{
	/// <summary>
	/// Whether a property of an object is allowed, given the property it depends on, if any.
	/// </summary>
	public static bool IsAllowed(object item, string property, object? parent = null) =>
		item.GetType().GetProperty(property)?.GetCustomAttribute<AllowedForAttribute>() is not { } allowed ||
		IsMet(allowed.OnParent ? parent : item, allowed.Property, allowed.Values, allowed.Unset);

	/// <summary>
	/// Whether a property of an object is required by the property it depends on, if any.
	/// </summary>
	public static bool IsRequired(object item, string property, object? parent = null) =>
		item.GetType().GetProperty(property)?.GetCustomAttribute<RequiredForAttribute>() is { } required &&
		IsMet(required.OnParent ? parent : item, required.Property, required.Values, required.Unset);

	/// <summary>
	/// Gets the rule for when a property of a class is allowed, as written in the data, or null if it's always allowed.
	/// </summary>
	public static DataRule? GetAllowedRule(Type type, string property) =>
		type.GetProperty(property)?.GetCustomAttribute<AllowedForAttribute>()?.CreateRule(type);

	/// <summary>
	/// Gets the rule for when a property of a class is required, as written in the data, or null if no other property
	/// requires it.
	/// </summary>
	public static DataRule? GetRequiredRule(Type type, string property) =>
		type.GetProperty(property)?.GetCustomAttribute<RequiredForAttribute>()?.CreateRule(type);

	/// <summary>
	/// Checks a property of an object against the rules marked on it: it's set without being allowed, or isn't set while
	/// it's allowed and required (by its mark, or when the caller says it's required in this case too). A property that's
	/// required is only required when it's allowed.
	/// </summary>
	public static DataRuleViolation Check(object item, string property, bool isSet, bool isRequired = false, object? parent = null)
	{
		var isAllowed = IsAllowed(item, property, parent);
		return isSet && !isAllowed ? DataRuleViolation.NotAllowed :
			!isSet && isAllowed && (isRequired || IsRequired(item, property, parent)) ? DataRuleViolation.Missing :
			DataRuleViolation.None;
	}

	/// <summary>
	/// Gets the name of a property of a class as it's written in the data.
	/// </summary>
	public static string GetDataName(Type type, string property) =>
		type.GetProperty(property)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property);

	internal static DataRule CreateRule(Type type, string property, IReadOnlyList<object> values, bool isUnset, bool isOnParent)
	{
		// A rule on the parent can't know the parent's class, so its name is written the usual way.
		var name = isOnParent ? JsonNamingPolicy.CamelCase.ConvertName(property) : GetDataName(type, property);
		return new DataRule(name, values.Select(x => x is Enum ? JsonNamingPolicy.CamelCase.ConvertName(x.ToString()!) : x.ToString()!).ToList(), isUnset, isOnParent);
	}

	private static bool IsMet(object? item, string property, IReadOnlyList<object> values, bool isUnset)
	{
		var value = item?.GetType().GetProperty(property)?.GetValue(item);
		return values.Count == 0 ? (value is not null) != isUnset : value is not null && values.Contains(value);
	}
}

/// <summary>
/// How a property of the data breaks the rules marked on it, if it does.
/// </summary>
public enum DataRuleViolation
{
	None,

	/// <summary>
	/// It's set, but isn't allowed.
	/// </summary>
	NotAllowed,

	/// <summary>
	/// It isn't set, but is required.
	/// </summary>
	Missing,
}
