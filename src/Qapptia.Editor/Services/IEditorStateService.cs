using Qapptia.Editor.Models;

namespace Qapptia.Editor.Services;

/// <summary>
/// Contrato del servicio de persistencia del estado de sesión y configuración del editor.
/// </summary>
public interface IEditorStateService
{
    EditorState Load();
    void Save(EditorState state);

    /// <summary>
    /// Persistencia no bloqueante: congela el snapshot serializando en el hilo llamante
    /// y delega la escritura a disco a un escritor en segundo plano coalescido.
    /// </summary>
    void SaveDeferred(EditorState state);
}
