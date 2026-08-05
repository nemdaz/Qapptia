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
    }
}