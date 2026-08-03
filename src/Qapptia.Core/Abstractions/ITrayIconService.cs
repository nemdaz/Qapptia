namespace Qapptia.Core.Abstractions;

public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Inicializa y muestra el icono en la bandeja del sistema con el menú especificado.
    /// </summary>
    void Initialize(TrayMenuDefinition menu, string iconPath);
}
