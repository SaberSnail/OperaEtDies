using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.LoadGame;

/// <summary>
/// Lists the saved games, newest first, and loads or deletes the one chosen. A save made with different data or mods
/// is only loaded once the player confirms it, and a save is only deleted once the player confirms it. If a save can't
/// be loaded or deleted, the reason is shown.
/// </summary>
public sealed class LoadGameViewModel : ViewModelBase
{
	public LoadGameViewModel(GameData gameData, SaveSettings settings, Random random)
	{
		m_gameData = gameData;
		m_settings = settings;
		m_random = random;
		m_saves = new ObservableCollection<SavedGameViewModel>(ReadSaves(settings.Directory)
			.Select(x => new SavedGameViewModel(x.FilePath, x.Save, x.Error, x.Save is not null && x.Save.IsDataChanged(settings), Load, Delete))
			.OrderByDescending(x => x.SavedAt));
		Saves = new ReadOnlyObservableCollection<SavedGameViewModel>(m_saves);
	}

	public event EventHandler? BackRequested;

	/// <summary>
	/// Raised when a saved game has been loaded, with its world.
	/// </summary>
	public event EventHandler<GenericEventArgs<GameWorld>>? GameLoaded;

	public ReadOnlyObservableCollection<SavedGameViewModel> Saves { get; }

	public bool HasSaves => m_saves.Count != 0;

	/// <summary>
	/// The save waiting for the player to confirm loading it, because the data or mods changed since it was saved.
	/// </summary>
	public SavedGameViewModel? ConfirmingLoad
	{
		get => VerifyAccess(m_confirmingLoad);
		private set
		{
			if (SetPropertyField(value, ref m_confirmingLoad))
				RaisePropertyChanged(nameof(IsConfirming));
		}
	}

	/// <summary>
	/// The save waiting for the player to confirm deleting it.
	/// </summary>
	public SavedGameViewModel? ConfirmingDelete
	{
		get => VerifyAccess(m_confirmingDelete);
		private set
		{
			if (SetPropertyField(value, ref m_confirmingDelete))
				RaisePropertyChanged(nameof(IsConfirming), nameof(DeleteConfirmation));
		}
	}

	/// <summary>
	/// Whether the player is being asked to confirm loading or deleting a save.
	/// </summary>
	public bool IsConfirming => ConfirmingLoad is not null || ConfirmingDelete is not null;

	/// <summary>
	/// Asks whether to delete the save waiting to be deleted.
	/// </summary>
	public string? DeleteConfirmation => m_confirmingDelete is { } save ? OurResources.DeleteSaveConfirmation.FormatCurrentCulture(save.Title, save.Details) : null;

	/// <summary>
	/// Why the last save couldn't be loaded or deleted, or null.
	/// </summary>
	public string? Error
	{
		get => VerifyAccess(m_error);
		private set
		{
			if (SetPropertyField(value, ref m_error))
				RaisePropertyChanged(nameof(HasError));
		}
	}

	public bool HasError => Error is not null;

	public void ConfirmLoad()
	{
		VerifyAccess();
		if (m_confirmingLoad is { } save)
		{
			ConfirmingLoad = null;
			LoadCore(save);
		}
	}

	public void ConfirmDelete()
	{
		VerifyAccess();
		if (m_confirmingDelete is { } save)
		{
			ConfirmingDelete = null;
			DeleteCore(save);
		}
	}

	/// <summary>
	/// Stops asking to load or delete a save, without doing it.
	/// </summary>
	public void Cancel()
	{
		VerifyAccess();
		ConfirmingLoad = null;
		ConfirmingDelete = null;
	}

	public void Back()
	{
		VerifyAccess();
		BackRequested.Raise(this);
	}

	private void Load(SavedGameViewModel save)
	{
		Error = null;
		ConfirmingDelete = null;
		if (save.IsDataChanged)
			ConfirmingLoad = save;
		else
			LoadCore(save);
	}

	private void Delete(SavedGameViewModel save)
	{
		Error = null;
		ConfirmingLoad = null;
		ConfirmingDelete = save;
	}

	private void LoadCore(SavedGameViewModel saved)
	{
		if (saved.Save is { } save)
		{
			try
			{
				var world = save.Load(m_gameData, m_settings.GameTitle, m_random);
				Log.Info($"Loaded {saved.FilePath}");
				GameLoaded.Raise(this, new GenericEventArgs<GameWorld>(world));
			}
			catch (SaveLoadException e)
			{
				Log.Error($"Couldn't load {saved.FilePath}: {e.Message}");
				Error = e.Message;
			}
		}
	}

	private void DeleteCore(SavedGameViewModel save)
	{
		try
		{
			File.Delete(save.FilePath);
			Log.Info($"Deleted {save.FilePath}");
			m_saves.Remove(save);
			RaisePropertyChanged(nameof(HasSaves));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.Error($"Couldn't delete {save.FilePath}: {e.Message}");
			Error = OurResources.DeleteSaveFailed.FormatCurrentCulture(e.Message);
		}
	}

	private static IEnumerable<(string FilePath, SaveFile? Save, string? Error)> ReadSaves(string directory)
	{
		var filePaths = Directory.Exists(directory) ? Directory.GetFiles(directory, "*" + SaveFile.Extension) : [];
		foreach (var filePath in filePaths)
		{
			SaveFile? save = null;
			string? error = null;
			try
			{
				save = SaveFile.Read(filePath);
			}
			catch (SaveLoadException e)
			{
				error = e.Message;
			}

			yield return (filePath, save, error);
		}
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(LoadGameViewModel));

	private readonly GameData m_gameData;
	private readonly SaveSettings m_settings;
	private readonly Random m_random;
	private readonly ObservableCollection<SavedGameViewModel> m_saves;
	private SavedGameViewModel? m_confirmingLoad;
	private SavedGameViewModel? m_confirmingDelete;
	private string? m_error;
}

/// <summary>
/// One saved game in the list: who the player is and when it was saved (to the second), or why the file couldn't be
/// read.
/// </summary>
public sealed class SavedGameViewModel : ViewModelBase
{
	public SavedGameViewModel(string filePath, SaveFile? save, string? error, bool isDataChanged, Action<SavedGameViewModel> load, Action<SavedGameViewModel> delete)
	{
		FilePath = filePath;
		Save = save;
		Error = error;
		IsDataChanged = isDataChanged;
		m_load = load;
		m_delete = delete;
		SavedAt = save?.SavedAt ?? new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
		Title = save?.PlayerName ?? Path.GetFileNameWithoutExtension(filePath);
		Details = save is null
			? error ?? ""
			: OurResources.SavedGameTime.FormatCurrentCulture(SavedAt.ToLocalTime().ToString("G", CultureInfo.CurrentCulture));
	}

	public string FilePath { get; }

	/// <summary>
	/// The save, or null if the file couldn't be read.
	/// </summary>
	public SaveFile? Save { get; }

	public string? Error { get; }

	/// <summary>
	/// Whether the data or mods have changed since the game was saved.
	/// </summary>
	public bool IsDataChanged { get; }

	public DateTimeOffset SavedAt { get; }

	public string Title { get; }

	/// <summary>
	/// When the game was saved, or why the file couldn't be read.
	/// </summary>
	public string Details { get; }

	public bool CanLoad() => Save is not null;

	public void Load()
	{
		VerifyAccess();
		if (CanLoad())
			m_load(this);
	}

	/// <summary>
	/// Asks to delete the save. Saves that can't be read can be deleted too.
	/// </summary>
	public void Delete()
	{
		VerifyAccess();
		m_delete(this);
	}

	private readonly Action<SavedGameViewModel> m_load;
	private readonly Action<SavedGameViewModel> m_delete;
}
