using Qapptia.Editor.Models;

namespace Qapptia.Editor.Services;

/// <summary>
/// Contrato del servicio de persistencia del estado de sesión y configuración del editor.
/// </summary>
public interface IEditorStateService
{
    EditorState Load();
    void Save(EditorState state);
}
