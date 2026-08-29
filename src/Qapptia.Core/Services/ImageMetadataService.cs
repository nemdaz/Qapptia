using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Qapptia.Core.Services;

/// <summary>
/// Servicio responsable de la inyección y extracción de metadatos estandarizados (MediaId y MediaType) al final de los archivos de imagen.
/// </summary>
public static class ImageMetadataService
{
    /// <summary>
    /// Obtiene sincrónicamente los metadatos de la imagen verificando el final del archivo. Si no existen, genera un nuevo ID, los anexa y los retorna.
    /// </summary>
    public static (string MediaId, string MediaType) EnsureImageMetadata(string filePath, string? mediaType = null)
    {
        var (existingId, existingType) = GetImageMetadata(filePath);
        if (!string.IsNullOrEmpty(existingId))
        {
            return (existingId, existingType ?? Constants.ResolveMediaType(filePath));
        }

        string newId = Guid.NewGuid().ToString();
        string resolvedType = mediaType ?? Constants.ResolveMediaType(filePath);
        AppendMediaMetadata(filePath, newId, resolvedType);
        return (newId, resolvedType);
    }

    /// <summary>
    /// Obtiene asincrónicamente los metadatos de la imagen verificando el final del archivo. Si no existen, genera un nuevo ID, los anexa y los retorna.
    /// </summary>
    public static async Task<(string MediaId, string MediaType)> EnsureImageMetadataAsync(string filePath, string? mediaType = null)
    {
        var (existingId, existingType) = await GetImageMetadataAsync(filePath);
        if (!string.IsNullOrEmpty(existingId))
        {
            return (existingId, existingType ?? Constants.ResolveMediaType(filePath));
        }

        string newId = Guid.NewGuid().ToString();
        string resolvedType = mediaType ?? Constants.ResolveMediaType(filePath);
        await AppendMediaMetadataAsync(filePath, newId, resolvedType);
        return (newId, resolvedType);
    }

    /// <summary>
    /// Lee sincrónicamente los últimos bytes del archivo para extraer el bloque de metadatos Qapptia.
    /// </summary>
    public static (string? MediaId, string? MediaType) GetImageMetadata(string filePath)
    {
        if (!File.Exists(filePath)) return (null, null);

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return (null, null);

            int bytesToRead = (int)Math.Min(Constants.MetadataBufferSize, fs.Length);
            fs.Seek(-bytesToRead, SeekOrigin.End);

            var buffer = new byte[bytesToRead];
            int bytesRead = fs.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0) return (null, null);

            string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string? mediaId = ExtractTagValue(content, Constants.MetadataMediaIdStart, Constants.MetadataMediaIdEnd);
            string? mediaType = ExtractTagValue(content, Constants.MetadataMediaTypeStart, Constants.MetadataMediaTypeEnd);

            return (mediaId, mediaType);
        }
        catch
        {
            // Fallar silenciosamente si el archivo está bloqueado o es inaccesible
            return (null, null);
        }
    }

    /// <summary>
    /// Lee asincrónicamente los últimos bytes del archivo para extraer el bloque de metadatos Qapptia.
    /// </summary>
    public static async Task<(string? MediaId, string? MediaType)> GetImageMetadataAsync(string filePath)
    {
        if (!File.Exists(filePath)) return (null, null);

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return (null, null);

            int bytesToRead = (int)Math.Min(Constants.MetadataBufferSize, fs.Length);
            fs.Seek(-bytesToRead, SeekOrigin.End);

            var buffer = new byte[bytesToRead];
            int bytesRead = await fs.ReadAsync(buffer.AsMemory(0, bytesToRead));
            if (bytesRead == 0) return (null, null);

            string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string? mediaId = ExtractTagValue(content, Constants.MetadataMediaIdStart, Constants.MetadataMediaIdEnd);
            string? mediaType = ExtractTagValue(content, Constants.MetadataMediaTypeStart, Constants.MetadataMediaTypeEnd);

            return (mediaId, mediaType);
        }
        catch
        {
            // Fallar silenciosamente si el archivo está bloqueado o es inaccesible
            return (null, null);
        }
    }

    /// <summary>
    /// Anexa sincrónicamente el bloque de metadatos estandarizado al final del archivo de imagen.
    /// </summary>
    public static void AppendMediaMetadata(string filePath, string mediaId, string mediaType)
    {
        if (!File.Exists(filePath) || string.IsNullOrEmpty(mediaId) || string.IsNullOrEmpty(mediaType)) return;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            string payload = $"{Constants.MetadataBlockStart}{Constants.MetadataMediaIdStart}{mediaId}{Constants.MetadataMediaIdEnd}{Constants.MetadataMediaTypeStart}{mediaType}{Constants.MetadataMediaTypeEnd}{Constants.MetadataBlockEnd}";
            var bytes = Encoding.UTF8.GetBytes(payload);
            fs.Write(bytes);
        }
        catch
        {
            // Fallar silenciosamente si no hay permisos de escritura
        }
    }

    /// <summary>
    /// Anexa asincrónicamente el bloque de metadatos estandarizado al final del archivo de imagen.
    /// </summary>
    public static async Task AppendMediaMetadataAsync(string filePath, string mediaId, string mediaType)
    {
        if (!File.Exists(filePath) || string.IsNullOrEmpty(mediaId) || string.IsNullOrEmpty(mediaType)) return;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            string payload = $"{Constants.MetadataBlockStart}{Constants.MetadataMediaIdStart}{mediaId}{Constants.MetadataMediaIdEnd}{Constants.MetadataMediaTypeStart}{mediaType}{Constants.MetadataMediaTypeEnd}{Constants.MetadataBlockEnd}";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await fs.WriteAsync(bytes.AsMemory());
        }
        catch
        {
            // Fallar silenciosamente si no hay permisos de escritura
        }
    }

    private static string? ExtractTagValue(string content, string startTag, string endTag)
    {
        int startIndex = content.LastIndexOf(startTag, StringComparison.Ordinal);
        if (startIndex < 0) return null;

        int valueStart = startIndex + startTag.Length;
        int endIndex = content.IndexOf(endTag, valueStart, StringComparison.Ordinal);
        if (endIndex <= valueStart) return null;

        return content[valueStart..endIndex];
    }
}
