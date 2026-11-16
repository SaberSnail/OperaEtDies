using System.Text.Json.Nodes;
using ModEditor.Model;

namespace ModEditor.ViewModels;

/// <summary>
/// One difference in the Changes tab: where it is, what happened, the value before and after, and the parts of the
/// mod that changed it (the last of them is the one whose value it has).
/// </summary>
public sealed class ChangeRowViewModel(DataChange change, IReadOnlyList<ChangeSourceViewModel> sources)
{
	public string Path { get; } = change.Path;

	public ChangeKind Kind { get; } = change.Kind;

	public string KindText { get; } = change.Kind switch
	{
		ChangeKind.Added => "Added",
		ChangeKind.Removed => "Removed",
		_ => "Changed",
	};

	public string? Before { get; } = Show(change.Before);

	public string? After { get; } = Show(change.After);

	public IReadOnlyList<ChangeSourceViewModel> Sources { get; } = sources;

	public bool HasSources => Sources.Count != 0;

	// Long values are cut short; the JSON tab has them in full.
	private static string? Show(JsonNode? value)
	{
		if (value is null)
			return null;
		var text = ModJson.Format(value);
		return text.Length > 600 ? text[..600] + "…" : text;
	}
}

/// <summary>
/// A part of the mod that made a change, which can be gone to in the Mod tab.
/// </summary>
public sealed class ChangeSourceViewModel(string title, Action goTo)
{
	public string Title { get; } = title;

	public void GoTo() => goTo();
}
