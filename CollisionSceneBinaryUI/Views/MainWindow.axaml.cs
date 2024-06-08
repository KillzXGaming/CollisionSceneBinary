using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CollisionSceneBinaryTool;
using CollisionSceneBinaryUI.ViewModels;
using System.Collections.Generic;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace CollisionSceneBinaryUI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OpenFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);

            var ext = new FilePickerFileType("Files")
            {
                Patterns = new[] { "*.dae", "*.csb", "*.zst" },
                MimeTypes = new[] { "Collada DAE", "Collision Scene Binary" },
                AppleUniformTypeIdentifiers = new[] { "" }
            };

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open DAE or CSB Files",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>() { ext },
            });

            bool as_big_endian = ViewModel.SelectedGame == MainWindowViewModel.ActiveGame.ColorSplash;

            foreach (var file in files)
            {
                //path to back save as
                ViewModel.FileName = file.Path.LocalPath;

                if (file.Name.EndsWith("csb.zst"))
                    ViewModel.LoadCsbFile(new CsbFile(Zstd.Decompress(file.Path.LocalPath), as_big_endian));
                else if (file.Name.EndsWith("csb"))
                    ViewModel.LoadCsbFile(new CsbFile(File.OpenRead(file.Path.LocalPath), as_big_endian));
                else if (file.Name.EndsWith("dae"))
                {
                    var imported = CsbImporter.ImportFromDae(file.Path.LocalPath);
                    ViewModel.LoadCtbFile(imported.CollisionTable);
                    ViewModel.LoadCsbFile(imported.CollisionScene);
                }
            } 
        }
    
        private async void Export_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ViewModel.FileName))
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);

            var ext = new FilePickerFileType("Collada DAE")
            {
                Patterns = new[] { "*.dae", },
                MimeTypes = new[] { "Collada DAE" },
                AppleUniformTypeIdentifiers = new[] { "" }
            };

            // Start async operation to open the dialog.
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save As DAE",
                SuggestedFileName = Path.GetFileName(ViewModel.FileName.Replace(".csb.zst", ".dae")),
                DefaultExtension = $".dae",
                FileTypeChoices = new List<FilePickerFileType>() { ext },
            });

            if (file != null)
                ViewModel.ExportScene(file.Path.LocalPath);
        }

        private async void SaveFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var ext = new FilePickerFileType("ZSTD Compressed Scene Binary")
            {
                Patterns = new[] { "*", },
                MimeTypes = new[] { "Scene Binary" },
                AppleUniformTypeIdentifiers = new[] { "" }
            };

            // Start async operation to open the dialog.
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save As Collision Scene Binary",
                SuggestedFileName = Path.GetFileNameWithoutExtension(ViewModel.FileName),
                DefaultExtension = $"",
                FileTypeChoices = new List<FilePickerFileType>() { ext },
            });

            if (file != null)
                ViewModel.SaveScene(file.Path.LocalPath);
        }

        private void Buttom_RemoveFlag_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var item = (string)button.Tag;
                ViewModel.RemoveColFlag(item);
            }
        }

        private void SelectGame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            gameSelect1.IsSelected = e.Source == gameSelect1;
            gameSelect2.IsSelected = e.Source == gameSelect2;
            gameSelect3.IsSelected = e.Source == gameSelect3;

            if (gameSelect1.IsSelected)
                ViewModel.SelectedGame = MainWindowViewModel.ActiveGame.PMTTYD;
            if (gameSelect2.IsSelected)
                ViewModel.SelectedGame = MainWindowViewModel.ActiveGame.OrigamiKing;
            if (gameSelect3.IsSelected) 
                ViewModel.SelectedGame = MainWindowViewModel.ActiveGame.ColorSplash;

            gameSelectorDropdown.Header = $"Game Select [{ViewModel.SelectedGame}]";

            ViewModel.ReloadGame();
        }

        private void Window_Loaded_1(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            gameSelectorDropdown.Header = $"Game Select [{ViewModel.SelectedGame}]";
        }
    }
}