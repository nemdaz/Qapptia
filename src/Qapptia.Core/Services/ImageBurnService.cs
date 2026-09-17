using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Qapptia.Core.Services;

/// <summary>
/// Servicio responsable de la persistencia de imágenes quemadas y generación de backups comprimidos.
/// </summary>
public static class ImageBurnService
{
    /// <summary>
    /// Crea un backup comprimido (.bak.gz) del archivo original antes de quemar anotaciones.
    /// </summary>
    public static async Task<string> CreateCompressedBackupAsync(string filePath, string mediaId)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("El archivo original no existe.", filePath);
        }

        string parentDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        string fileName = Path.GetFileName(filePath);
        string dibujoDir = Path.Combine(parentDir, Constants.DrawingExtension);

        if (!Directory.Exists(dibujoDir))
        {
            Directory.CreateDirectory(dibujoDir);
            File.SetAttributes(dibujoDir, File.GetAttributes(dibujoDir) | FileAttributes.Hidden);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string backupName = $"{fileName}_{mediaId}_{timestamp}.bak.gz";
        string backupPath = Path.Combine(dibujoDir, backupName);

        using (var originalStream = File.OpenRead(filePath))
        using (var backupStream = File.Create(backupPath))
        using (var gzStream = new GZipStream(backupStream, CompressionLevel.Optimal))
        {
            await originalStream.CopyToAsync(gzStream);
        }

        return backupPath;
    }

    /// <summary>
    /// Guarda los bytes finales de la imagen quemada en disco, preservando la fecha de creación original (ISO 16684-1),
    /// registrando la fecha de modificación y garantizando la adopción oficial de imágenes externas.
    /// </summary>
    public static async Task SaveBurnedImageAsync(
        string filePath,
        byte[] pngBytes,
        string? mediaId,
        string? mediaType = null,
        DateTime? createdAt = null,
        DateTime? modifyDate = null)
    {
        DateTime? originalCreationTime = null;
        DateTime? resolvedCreatedAt = createdAt;
        string resolvedMediaId = mediaId ?? string.Empty;

        if (File.Exists(filePath))
        {
            try
            {
                originalCreationTime = File.GetCreationTimeUtc(filePath);

                if (!resolvedCreatedAt.HasValue || resolvedCreatedAt.Value <= DateTime.MinValue)
                {
                    var (existingId, _, existingDate) = ImageMetadataService.GetImageMetadata(filePath);
                    if (string.IsNullOrEmpty(resolvedMediaId) && !string.IsNullOrEmpty(existingId))
                    {
                        resolvedMediaId = existingId;
                    }

                    resolvedCreatedAt = (existingDate.HasValue && existingDate.Value > DateTime.MinValue)
                        ? existingDate.Value
                        : originalCreationTime;
                }
            }
            catch
            {
                // Fallback silencioso ante bloqueo transitorio de archivo
            }
        }

        // Si es una imagen externa sin MediaId previo, adoptarla con un nuevo GUID
        if (string.IsNullOrWhiteSpace(resolvedMediaId))
        {
            resolvedMediaId = Guid.NewGuid().ToString();
        }

        resolvedCreatedAt ??= (originalCreationTime ?? DateTime.UtcNow);
        DateTime resolvedModifyDate = modifyDate ?? DateTime.UtcNow;

        string resolvedType = mediaType ?? Constants.ResolveMediaType(filePath);
        byte[] finalBytes = ImageMetadataService.InjectMetadata(
            pngBytes,
            resolvedMediaId,
            resolvedType,
            resolvedCreatedAt,
            resolvedModifyDate);

        await File.WriteAllBytesAsync(filePath, finalBytes);

        if (originalCreationTime.HasValue && originalCreationTime.Value > DateTime.MinValue)
        {
            try
            {
                File.SetCreationTimeUtc(filePath, originalCreationTime.Value);
            }
            catch
            {
            }
        }

        ImageMetadataService.InvalidateEffectiveDateCache(filePath);
    }
}
