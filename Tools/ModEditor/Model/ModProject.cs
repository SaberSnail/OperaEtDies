using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace ModEditor.Model;

/// <summary>
/// The state of a mod's files and folders at one moment, for undoing changes and telling whether there are any.
/// </summary>
public sealed record ModSnapshot(IReadOnlyList<(string Path, string Text)> Files, IReadOnlyList<string> Folders)
{
	public bool IsSameAs(ModSnapshot other) =>
		Files.Count == other.Files.Count && Folders.Count == other.Folders.Count &&
		Files.Zip(other.Files).All(x => x.First.Path == x.Second.Path && x.First.Text == x.Second.Text) &&
		Folders.Zip(other.Folders).All(x => x.First == x.Second);
}

/// <summary>
/// A mod folder being edited: its JSON files (including those in subfolders) and its folders. Changes are kept in
/// memory until they're saved.
/// </summary>
public sealed class ModProject
{
	/// <summary>
	/// Loads every JSON file and folder in the mod folder.
	/// </summary>
	public static ModProject Load(string directory)
	{
		var files = System.IO.Directory.EnumerateFiles(directory, "*.json", System.IO.SearchOption.AllDirectories)
			.Select(x => (ModFilePath.FromSystemPath(directory, x), File.ReadAllText(x)));
		var folders = System.IO.Directory.EnumerateDirectories(directory, "*", System.IO.SearchOption.AllDirectories)
			.Select(x => ModFilePath.FromSystemPath(directory, x));
		return new ModProject(directory, files, folders);
	}

	public ModProject(string directory, IEnumerable<(string Path, string Text)> files, IEnumerable<string> folders)
	{
		Directory = directory;
		m_files = Sorted(files.Select(x => new ModFile(x.Path, x.Text)));
		m_folders = new HashSet<string>(folders, ModFilePath.Comparer);

		m_saved = TakeSnapshot();
	}

	/// <summary>
	/// The mod folder's full path.
	/// </summary>
	public string Directory { get; }

	public string Name => Path.GetFileName(Path.TrimEndingDirectorySeparator(Directory));

	/// <summary>
	/// The files, in the order the game applies them.
	/// </summary>
	public IReadOnlyList<ModFile> Files => m_files;

	/// <summary>
	/// Every folder in the mod folder (not counting the mod folder itself), including empty ones.
	/// </summary>
	public IReadOnlyCollection<string> Folders => m_folders;

	public bool IsModified => !TakeSnapshot().IsSameAs(m_saved);

	public ModFile? FindFile(string path) => m_files.FirstOrDefault(x => ModFilePath.AreSame(x.Path, path));

	public bool FolderExists(string path) => path.Length == 0 || m_folders.Contains(path);

	/// <summary>
	/// Whether a file or folder already has the path.
	/// </summary>
	public bool Exists(string path) => FindFile(path) is not null || FolderExists(path);

	public ModFile AddFile(string path, string text)
	{
		var file = new ModFile(path, text);
		m_files = Sorted(m_files.Append(file));
		AddFolder(ModFilePath.GetParent(path));
		return file;
	}

	public void RemoveFile(ModFile file) => m_files = m_files.Where(x => x != file).ToList();

	public void MoveFile(ModFile file, string path)
	{
		file.Path = path;
		AddFolder(ModFilePath.GetParent(path));
		m_files = Sorted(m_files);
	}

	/// <summary>
	/// Adds a folder, and the folders it's in.
	/// </summary>
	public void AddFolder(string path)
	{
		for (var folder = path; folder.Length != 0; folder = ModFilePath.GetParent(folder))
			m_folders.Add(folder);
	}

	/// <summary>
	/// Removes a folder and everything in it.
	/// </summary>
	public void RemoveFolder(string path)
	{
		m_files = m_files.Where(x => !ModFilePath.IsWithin(x.Path, path)).ToList();
		m_folders.RemoveWhere(x => ModFilePath.IsWithin(x, path));
	}

	/// <summary>
	/// Moves a folder and everything in it to a new path.
	/// </summary>
	public void MoveFolder(string path, string newPath)
	{
		foreach (var file in m_files.Where(x => ModFilePath.IsWithin(x.Path, path)))
			file.Path = ModFilePath.Rebase(file.Path, path, newPath);
		var folders = m_folders.Where(x => ModFilePath.IsWithin(x, path)).ToList();
		foreach (var folder in folders)
			m_folders.Remove(folder);
		foreach (var folder in folders)
			m_folders.Add(ModFilePath.Rebase(folder, path, newPath));
		AddFolder(newPath);
		m_files = Sorted(m_files);
	}

	public ModSnapshot TakeSnapshot() => new(
		m_files.Select(x => (x.Path, x.Text)).ToList(),
		m_folders.Order(StringComparer.Ordinal).ToList());

	public void Restore(ModSnapshot snapshot)
	{
		m_files = Sorted(snapshot.Files.Select(x => new ModFile(x.Path, x.Text)));
		m_folders.Clear();
		m_folders.UnionWith(snapshot.Folders);

	}

	/// <summary>
	/// Writes the changes to the mod folder. Files and folders that were removed go to the recycle bin, and only files
	/// whose text changed are written.
	/// </summary>
	public void Save()
	{
		var current = TakeSnapshot();
		var savedFiles = m_saved.Files.ToDictionary(x => x.Path, x => x.Text, ModFilePath.Comparer);
		var currentPaths = new HashSet<string>(current.Files.Select(x => x.Path), ModFilePath.Comparer);

		foreach (var (path, _) in m_saved.Files.Where(x => !currentPaths.Contains(x.Path)))
			Recycle(ModFilePath.ToSystemPath(Directory, path), isFolder: false);

		foreach (var folder in current.Folders)
			System.IO.Directory.CreateDirectory(ModFilePath.ToSystemPath(Directory, folder));

		foreach (var (path, text) in current.Files)
		{
			var systemPath = ModFilePath.ToSystemPath(Directory, path);
			if (savedFiles.TryGetValue(path, out var savedText))
			{
				// A file renamed only by case keeps its old name on disk unless it's moved.
				var savedPath = m_saved.Files.First(x => ModFilePath.AreSame(x.Path, path)).Path;
				if (savedPath != path)
				{
					var temporaryPath = systemPath + ".renaming";
					File.Move(ModFilePath.ToSystemPath(Directory, savedPath), temporaryPath);
					File.Move(temporaryPath, systemPath);
				}

				if (savedText == text)
					continue;
			}

			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(systemPath)!);
			File.WriteAllText(systemPath, text);
		}

		// Folders that were removed are recycled from the deepest up, once they're empty.
		var currentFolders = new HashSet<string>(current.Folders, ModFilePath.Comparer);
		foreach (var folder in m_saved.Folders.Where(x => !currentFolders.Contains(x)).OrderByDescending(x => x.Length))
		{
			var systemPath = ModFilePath.ToSystemPath(Directory, folder);
			if (System.IO.Directory.Exists(systemPath) && !System.IO.Directory.EnumerateFileSystemEntries(systemPath).Any())
				Recycle(systemPath, isFolder: true);
		}

		m_saved = current;
	}

	private static void Recycle(string systemPath, bool isFolder)
	{
		if (isFolder && System.IO.Directory.Exists(systemPath))
			FileSystem.DeleteDirectory(systemPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
		else if (!isFolder && File.Exists(systemPath))
			FileSystem.DeleteFile(systemPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
	}

	private static IReadOnlyList<ModFile> Sorted(IEnumerable<ModFile> files) => files.OrderBy(x => x.Path, StringComparer.Ordinal).ToList();

	private IReadOnlyList<ModFile> m_files;
	private readonly HashSet<string> m_folders;
	private ModSnapshot m_saved;
}
