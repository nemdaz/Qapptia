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
    /// Guarda los bytes finales de la imagen quemada en disco y preserva sus metadatos MediaId y MediaType.
    /// </summary>
    public static async Task SaveBurnedImageAsync(string filePath, byte[] pngBytes, string? mediaId, string? mediaType = null)
    {
        await File.WriteAllBytesAsync(filePath, pngBytes);

        if (!string.IsNullOrEmpty(mediaId))
        {
            string resolvedType = mediaType ?? Constants.ResolveMediaType(filePath);
            await ImageMetadataService.AppendMediaMetadataAsync(filePath, mediaId, resolvedType);
        }
    }

    /// <summary>
    /// Recorta un arreglo de bytes de imagen (PNG) utilizando SkiaSharp.
    /// Esto mueve la manipulación de memoria gráfica al backend/core.
    /// </summary>
    public static byte[] CropImageBytesIfNeeded(byte[] originalPng, int left, int top, int right, int bottom)
    {
        if (right <= left || bottom <= top)
            return originalPng;

        using var bitmap = SkiaSharp.SKBitmap.Decode(originalPng);
        var skRect = new SkiaSharp.SKRectI(left, top, right, bottom);

        using var cropped = new SkiaSharp.SKBitmap(skRect.Width, skRect.Height);
        if (bitmap.ExtractSubset(cropped, skRect))
        {
            using var data = cropped.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        return originalPng;
    }
}
