using Swarnakshi.Application.Abstractions;

namespace Swarnakshi.Infrastructure.Storage;

/// <summary>Default IFileStorage. Swap for blob/S3 without touching business logic.</summary>
public sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var safe = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var full = Path.Combine(rootPath, safe);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return safe;
    }

    public Task<Stream> OpenAsync(string storagePath, CancellationToken ct = default)
        => Task.FromResult<Stream>(File.OpenRead(Path.Combine(rootPath, storagePath)));

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var full = Path.Combine(rootPath, storagePath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}
