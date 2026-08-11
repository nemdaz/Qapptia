using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Qapptia.Core.Services;

public static class ImageMetadataService
{

    /// <summary>
    /// Obtiene el ID único de la imagen verificando el final del archivo.
    /// Si no existe, genera uno nuevo, lo anexa y lo retorna.
    /// </summary>
    public static async Task<string> EnsureImageIdAsync(string filePath)
    {
        var id = await GetImageIdAsync(filePath);
        if (!string.IsNullOrEmpty(id))
            return id;

        // Si no hay ID, genera uno y lo anexa
        id = Guid.NewGuid().ToString();
        await AppendImageIdAsync(filePath, id);
        return id;
    }

    /// <summary>
    /// Lee los últimos 128 bytes del archivo para encontrar el payload QapptiaID.
    /// Retorna null si no se encuentra.
    /// </summary>
    public static async Task<string?> GetImageIdAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return null;

            int bytesToRead = (int)Math.Min(128, fs.Length);
            fs.Seek(-bytesToRead, SeekOrigin.End);

            var buffer = new byte[bytesToRead];
            int bytesRead = await fs.ReadAsync(buffer.AsMemory(0, bytesToRead));
            if (bytesRead == 0) return null;

            string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            int startIndex = content.LastIndexOf(AppConstants.MetadataTagStart, StringComparison.Ordinal);
            if (startIndex >= 0)
            {
                int endIndex = content.IndexOf(AppConstants.MetadataTagEnd, startIndex, StringComparison.Ordinal);
                if (endIndex > startIndex)
                {
                    return content.Substring(startIndex + AppConstants.MetadataTagStart.Length, endIndex - startIndex - AppConstants.MetadataTagStart.Length);
                }
            }
        }
        catch
        {
            // Fallar silenciosamente si el archivo está en uso u ocurre un error de lectura
        }

        return null;
    }

    /// <summary>
    /// Anexa el payload QapptiaID al final del archivo.
    /// </summary>
    public static async Task AppendImageIdAsync(string filePath, string id)
    {
        if (!File.Exists(filePath) || string.IsNullOrEmpty(id)) return;

        try
        {
            // Usamos FileShare.Read para no bloquear a otros procesos que solo lean
            using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            string payload = $"{AppConstants.MetadataTagStart}{id}{AppConstants.MetadataTagEnd}";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await fs.WriteAsync(bytes, 0, bytes.Length);
        }
        catch
        {
            // Fallar silenciosamente si no hay permisos o el archivo está bloqueado
        }
    }
}
