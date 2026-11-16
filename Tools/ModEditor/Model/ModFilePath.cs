namespace ModEditor.Model;

/// <summary>
/// Paths of files and folders within a mod folder. They use forward slashes, and the mod folder itself is "".
/// </summary>
public static class ModFilePath
{
	public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

	public static string GetName(string path) => path.Length == 0 ? "" : path[(path.LastIndexOf('/') + 1)..];

	public static string GetParent(string path) => path.LastIndexOf('/') is var index and >= 0 ? path[..index] : "";

	public static string Combine(string folder, string name) => folder.Length == 0 ? name : $"{folder}/{name}";

	public static bool AreSame(string a, string b) => Comparer.Equals(a, b);

	/// <summary>
	/// Whether the path is the folder or anything in it.
	/// </summary>
	public static bool IsWithin(string path, string folder) =>
		folder.Length == 0 || AreSame(path, folder) || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Moves a path that's within a folder to the same place within another folder.
	/// </summary>
	public static string Rebase(string path, string oldFolder, string newFolder) =>
		AreSame(path, oldFolder) ? newFolder : Combine(newFolder, path[(oldFolder.Length + (oldFolder.Length == 0 ? 0 : 1))..]);

	/// <summary>
	/// The key that sorts a file or folder among its siblings in the order the game applies mod files: by path, with
	/// forward slashes, comparing ordinally.
	/// </summary>
	public static string GetSortKey(string name, bool isFolder) => isFolder ? name + "/" : name;

	/// <summary>
	/// Checks a file or folder name typed by the user, returning the problem with it, or null if there isn't one.
	/// </summary>
	public static string? CheckName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return "The name can't be empty.";
		if (name != name.Trim())
			return "The name can't start or end with a space.";
		if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
			return "The name can't contain any of \\ / : * ? \" < > |.";
		if (name is "." or ".." || name.EndsWith('.'))
			return "The name can't end with a period.";
		return null;
	}

	/// <summary>
	/// Adds ".json" to a file name that doesn't end with it, since only JSON files are loaded.
	/// </summary>
	public static string EnsureJsonExtension(string name) => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json";

	public static string ToSystemPath(string root, string path) => path.Length == 0 ? root : System.IO.Path.Combine(root, path.Replace('/', System.IO.Path.DirectorySeparatorChar));

	public static string FromSystemPath(string root, string systemPath) => System.IO.Path.GetRelativePath(root, systemPath).Replace('\\', '/');
}
