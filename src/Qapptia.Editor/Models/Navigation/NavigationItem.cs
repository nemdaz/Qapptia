using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Qapptia.Editor.Models.Navigation;

/// <summary>
/// Clase base para cualquier elemento navegable del explorador de capturas.
/// </summary>
public abstract partial class NavigationItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Ext => System.IO.Path.GetExtension(Name);
    public string FullPath { get; set; } = string.Empty;

    // Propiedad pura MVVM para identificar si el nodo es una carpeta en tiempo de Binding UI
    public bool IsFolder => this is GroupItem;

    /// <summary>
    /// Vacío confirmado por el indexador: el nodo fue inspeccionado y no contiene elementos.
    /// Gobierna la ocultación del chevron y la atenuación visual de la fila.
    /// </summary>
    public virtual bool IsEmptyConfirmed => false;

    public DateTime EffectiveDateUtc { get; set; } = DateTime.MinValue;
    public GroupItem? Parent { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Profundidad matemática calculada para sangría reactiva en Flat Virtual Tree.
    /// </summary>
    public int Depth => Parent == null ? 0 : Parent.Depth + 1;

    /// <summary>
    /// Margen de sangría calculado para Flat Tree virtualizado.
    /// Indentación uniforme de 16 px por nivel de profundidad.
    /// </summary>
    public virtual Avalonia.Thickness IndentMargin => new(Depth * 16, 0, 0, 0);
}
