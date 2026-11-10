using System.Collections.ObjectModel;
using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Mods;

namespace OperaEtDies.Controls.ManageMods;

/// <summary>
/// Lets the player choose the folders that mod files are loaded from, and the order they're applied in. Each change
/// is saved right away.
/// </summary>
public sealed class ManageModsViewModel : ViewModelBase
{
	public ManageModsViewModel(IModListStore store, IFolderPicker folderPicker, bool areModsEnabled)
	{
		m_store = store;
		m_folderPicker = folderPicker;
		AreModsEnabled = areModsEnabled;
		m_mods = new ObservableCollection<string>(store.Load());
		Mods = new ReadOnlyObservableCollection<string>(m_mods);
	}

	public event EventHandler? BackRequested;

	/// <summary>
	/// The mod folders, in the order their files are applied.
	/// </summary>
	public ReadOnlyObservableCollection<string> Mods { get; }

	public string? SelectedMod
	{
		get => VerifyAccess(m_selectedMod);
		set => SetPropertyField(value, ref m_selectedMod);
	}

	/// <summary>
	/// False if mods were turned off from the command line, so the list is kept but not loaded.
	/// </summary>
	public bool AreModsEnabled { get; }

	/// <summary>
	/// Asks the player for a folder and adds it to the end of the list. A folder that's already in the list is
	/// selected instead.
	/// </summary>
	public void AddMod()
	{
		VerifyAccess();
		var initialDirectory = m_selectedMod is null ? null : Path.GetDirectoryName(m_selectedMod);
		var folder = m_folderPicker.PickFolder(OurResources.SelectModFolder, initialDirectory);
		if (folder is not null)
		{
			var existing = m_mods.FirstOrDefault(x => string.Equals(NormalizePath(x), NormalizePath(folder), StringComparison.OrdinalIgnoreCase));
			if (existing is null)
			{
				m_mods.Add(folder);
				Save();
			}

			SelectedMod = existing ?? folder;
		}
	}

	public bool CanRemoveMod() => m_selectedMod is not null;

	/// <summary>
	/// Removes the selected folder, and selects the one that takes its place.
	/// </summary>
	public void RemoveMod()
	{
		VerifyAccess();
		if (!CanRemoveMod())
			return;

		var index = m_mods.IndexOf(m_selectedMod!);
		m_mods.RemoveAt(index);
		Save();
		SelectedMod = m_mods.Count == 0 ? null : m_mods[Math.Min(index, m_mods.Count - 1)];
	}

	public bool CanMoveModUp() => m_selectedMod is not null && m_mods.IndexOf(m_selectedMod) > 0;

	public void MoveModUp() => MoveSelectedMod(-1, CanMoveModUp());

	public bool CanMoveModDown() => m_selectedMod is not null && m_mods.IndexOf(m_selectedMod) < m_mods.Count - 1;

	public void MoveModDown() => MoveSelectedMod(1, CanMoveModDown());

	public void Back()
	{
		VerifyAccess();
		BackRequested.Raise(this);
	}

	private void MoveSelectedMod(int offset, bool canMove)
	{
		VerifyAccess();
		if (canMove)
		{
			var index = m_mods.IndexOf(m_selectedMod!);
			m_mods.Move(index, index + offset);
			Save();
		}
	}

	// Folders in the saved list may not be valid paths, since the registry can be edited by hand.
	private static string NormalizePath(string path)
	{
		try
		{
			return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
		}
		catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return path;
		}
	}

	private void Save() => m_store.Save(m_mods.ToList());

	private readonly IModListStore m_store;
	private readonly IFolderPicker m_folderPicker;
	private readonly ObservableCollection<string> m_mods;
	private string? m_selectedMod;
}
