using System;
using Avalonia;
using Avalonia.Controls;
using Qapptia.App.Editor.ViewModels;

namespace Qapptia.App.Editor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new EditorViewModel();
        DataContext = vm;
        vm.LoadSidebarImagesCommand.Execute(null);

        bool isInitialSizeSet = false;
        this.SizeChanged += (s, e) =>
        {
            var grid = this.FindControl<Grid>("MainGrid");
            if (grid != null && grid.ColumnDefinitions.Count >= 3)
            {
                var sidebarCol = grid.ColumnDefinitions[2];
                var width = e.NewSize.Width;
                sidebarCol.MinWidth = width * 0.10; // Mínimo 10%
                sidebarCol.MaxWidth = width * 0.50; // Máximo 50%
                
                if (!isInitialSizeSet && width > 0)
                {
                    sidebarCol.Width = new GridLength(width * 0.30);
                    isInitialSizeSet = true;
                }
                else
                {
                    // Si el ancho actual es menor que el mínimo, lo forzamos al mínimo
                    if (sidebarCol.Width.Value < sidebarCol.MinWidth)
                    {
                        sidebarCol.Width = new GridLength(sidebarCol.MinWidth);
                    }
                    // Si es mayor que el máximo, lo forzamos al máximo
                    else if (sidebarCol.Width.Value > sidebarCol.MaxWidth)
                    {
                        sidebarCol.Width = new GridLength(sidebarCol.MaxWidth);
                    }
                }
            }
        };
    }
}