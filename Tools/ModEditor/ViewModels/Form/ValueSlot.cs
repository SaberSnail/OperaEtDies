using System.Text.Json.Nodes;
using ModEditor.Model;

namespace ModEditor.ViewModels.Form;

/// <summary>
/// Where a field's value lives in the JSON being edited: a property of an object, an item of a list, or the whole
/// value.
/// </summary>
public abstract class ValueSlot
{
	public abstract JsonNode? Get();

	public abstract void Set(JsonNode? value);

	/// <summary>
	/// Whether there's a value there (an object property can be left out).
	/// </summary>
	public virtual bool Exists => true;

	/// <summary>
	/// Takes the value out: a property is removed, and anything else is set to null.
	/// </summary>
	public virtual void Remove() => Set(null);

	/// <summary>
	/// The object the value is a property of, or null if it isn't one.
	/// </summary>
	public virtual JsonObject? ParentObject => null;
}

public sealed class PropertySlot(JsonObject parent, string name) : ValueSlot
{
	public override JsonObject ParentObject => parent;

	public override bool Exists => ModJson.FindName(parent, name) is not null;

	public override JsonNode? Get() => ModJson.FindName(parent, name) is { } key ? parent[key] : null;

	public override void Set(JsonNode? value)
	{
		// A value that's already somewhere else (such as one moved between properties) is copied.
		value = value?.Parent is null ? value : value.DeepClone();
		parent[ModJson.FindName(parent, name) ?? name] = value;
	}

	public override void Remove()
	{
		if (ModJson.FindName(parent, name) is { } key)
			parent.Remove(key);
	}
}

public sealed class ItemSlot(JsonArray list, int index) : ValueSlot
{
	public override JsonNode? Get() => index < list.Count ? list[index] : null;

	public override void Set(JsonNode? value)
	{
		if (index < list.Count)
			list[index] = value?.Parent is null ? value : value.DeepClone();
	}
}

/// <summary>
/// The whole value of a form.
/// </summary>
public sealed class RootSlot(FormViewModel form) : ValueSlot
{
	public override JsonNode? Get() => form.Value;

	public override void Set(JsonNode? value) => form.Value = value;
}
