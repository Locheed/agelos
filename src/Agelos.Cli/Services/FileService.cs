namespace Agelos.Cli.Services;

public interface IFileService
{
    Task<bool> FileExistsAsync(string path);
    Task<bool> DirectoryExistsAsync(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string content);
    Task CreateDirectoryAsync(string path);
}

public class FileService : IFileService
{
    public Task<bool> FileExistsAsync(string path) => Task.FromResult(File.Exists(path));
    public Task<bool> DirectoryExistsAsync(string path) => Task.FromResult(Directory.Exists(path));
    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
    public Task WriteAllTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);

    public Task CreateDirectoryAsync(string path)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }
}
