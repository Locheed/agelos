using Spectre.Console;

namespace Agelos.Cli.Services;

public static class ModelDownloadService
{
    public static readonly string ModelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".agelos", "models");

    public static string ModelPath(string fileName) => Path.Combine(ModelsDir, fileName);

    public static bool IsDownloaded(string fileName) => File.Exists(ModelPath(fileName));

    public static IEnumerable<FileInfo> ListDownloaded() =>
        Directory.Exists(ModelsDir)
            ? new DirectoryInfo(ModelsDir).EnumerateFiles("*.gguf")
            : [];

    public static async Task<string> DownloadAsync(string hfRepo, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ModelsDir);
        var destPath = ModelPath(fileName);
        var tmpPath  = destPath + ".part";

        if (File.Exists(destPath))
        {
            AnsiConsole.MarkupLine($"[dim]Already downloaded: {fileName}[/]");
            return destPath;
        }

        if (File.Exists(tmpPath))
            File.Delete(tmpPath);

        var url = $"https://huggingface.co/{hfRepo}/resolve/main/{Uri.EscapeDataString(fileName)}";

        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0L;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]{fileName}[/]", maxValue: total > 0 ? total : 1);

                using var src  = await response.Content.ReadAsStreamAsync(ct);
                using var dest = File.Create(tmpPath);

                var buf      = new byte[65536];
                long received = 0;
                int  n;

                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dest.WriteAsync(buf.AsMemory(0, n), ct);
                    received += n;
                    if (total > 0) task.Value = received;
                }
            });

        File.Move(tmpPath, destPath, overwrite: true);
        return destPath;
    }
}
