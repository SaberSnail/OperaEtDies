using System.Diagnostics;
using System.Text.Json.Nodes;
using GoldenAnvil.Utility;

namespace PortraitEditor.Model;

/// <summary>
/// The portrait data being edited, as JSON (with the mods applied), along with the data as it was loaded and the
/// changes that can be undone. Edits change the JSON in place.
/// </summary>
public sealed class PortraitDocument
{
	public PortraitDocument(string filePath, JsonObject root)
	{
		FilePath = filePath;
		Root = root;
		Original = (JsonObject) root.DeepClone();
	}

	/// <summary>
	/// Raised after the data changes.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	/// Raised after the whole data is replaced, such as by undo, so anything holding parts of it must find them again.
	/// Changed is raised after it.
	/// </summary>
	public event EventHandler? Replaced;

	public string FilePath { get; }

	public JsonObject Root { get; private set; }

	/// <summary>
	/// The data as it was loaded.
	/// </summary>
	public JsonObject Original { get; }

	public bool IsModified => !JsonNode.DeepEquals(Root, Original);

	public bool CanUndo => m_undo.Count != 0;

	public bool CanRedo => m_redo.Count != 0;

	/// <summary>
	/// Gets a top level list, such as "layers", from the data or from the data as it was loaded.
	/// </summary>
	public static JsonArray? GetList(JsonObject root, string name) => root.GetProperty(name) as JsonArray;

	/// <summary>
	/// Makes a change that can be undone. Changes with the same merge key made close together, such as typing in a
	/// field or dragging a point, are undone together.
	/// </summary>
	public void Edit(string mergeKey, Action change)
	{
		if (mergeKey != m_lastMergeKey || m_lastEdit.Elapsed > s_mergeTime)
			m_undo.Push(Root.ToJsonString());
		m_lastMergeKey = mergeKey;
		m_lastEdit.Restart();
		m_redo.Clear();

		change();
		Changed.Raise(this);
	}

	/// <summary>
	/// Makes a change that can be undone, and that changes the structure of the data (such as adding or removing list
	/// items), so it's never merged with other changes.
	/// </summary>
	public void EditStructure(Action change)
	{
		BreakMerge();
		Edit("", change);
		BreakMerge();
		Replaced.Raise(this);
	}

	/// <summary>
	/// Starts a new undo step with the next change, even if it has the same merge key.
	/// </summary>
	public void BreakMerge() => m_lastMergeKey = null;

	public void Undo()
	{
		if (m_undo.TryPop(out var snapshot))
		{
			m_redo.Push(Root.ToJsonString());
			Restore(snapshot);
		}
	}

	public void Redo()
	{
		if (m_redo.TryPop(out var snapshot))
		{
			m_undo.Push(Root.ToJsonString());
			Restore(snapshot);
		}
	}

	private void Restore(string snapshot)
	{
		Root = (JsonObject) JsonNode.Parse(snapshot)!;
		BreakMerge();
		Replaced.Raise(this);
		Changed.Raise(this);
	}

	private static readonly TimeSpan s_mergeTime = TimeSpan.FromSeconds(1.5);

	private readonly Stack<string> m_undo = new();
	private readonly Stack<string> m_redo = new();
	private readonly Stopwatch m_lastEdit = new();
	private string? m_lastMergeKey;
}
