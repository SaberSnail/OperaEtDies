using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OperaEtDies.Data;

/// <summary>
/// Fingerprints folders of data files, so a saved game can tell whether the data or mods have changed since it was
/// saved. Any change to any file changes the hash, even one that doesn't matter, such as a comment.
/// </summary>
public static class DataFolderHash
{
	/// <summary>
	/// Hashes every file in each folder, in order. Each file's path within its
	/// folder is included, so renaming or moving a file changes the hash too. Folders that don't exist are skipped.
	/// </summary>
	public static string Compute(IEnumerable<string> directories)
	{
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		foreach (var directory in directories)
		{
			AppendText(hash, "folder");
			if (Directory.Exists(directory))
			{
				var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
					.Select(x => (Path: x, RelativePath: Path.GetRelativePath(directory, x).Replace('\\', '/')))
					.OrderBy(x => x.RelativePath, StringComparer.Ordinal);
				foreach (var (path, relativePath) in files)
				{
					var contents = File.ReadAllBytes(path);
					AppendText(hash, relativePath);
					AppendText(hash, contents.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
					hash.AppendData(contents);
				}
			}
		}

		return Convert.ToHexString(hash.GetHashAndReset());
	}

	private static void AppendText(IncrementalHash hash, string text)
	{
		hash.AppendData(Encoding.UTF8.GetBytes(text));
		hash.AppendData([0]);
	}
}
