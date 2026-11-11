using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Klimaatatlas.Gui.Views;

/// <summary>File pickers and "open with default app", via Avalonia's cross-platform storage/launcher APIs.</summary>
public interface IFileDialogs
{
    Task<string?> PickFileAsync(string title, string fileTypeName, string pattern, string? startPath);
    Task OpenWithDefaultAppAsync(string path);
}

public sealed class FileDialogs(TopLevel topLevel) : IFileDialogs
{
    public async Task<string?> PickFileAsync(string title, string fileTypeName, string pattern, string? startPath)
    {
        IStorageFolder? startFolder = null;
        var directory = startPath is null ? null : Path.GetDirectoryName(startPath);
        if (directory is not null && Directory.Exists(directory))
            startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(directory);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = [new FilePickerFileType(fileTypeName) { Patterns = [pattern] }, FilePickerFileTypes.All],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task OpenWithDefaultAppAsync(string path)
    {
        var file = await topLevel.StorageProvider.TryGetFileFromPathAsync(path);
        if (file is not null) await topLevel.Launcher.LaunchFileAsync(file);
    }
}
