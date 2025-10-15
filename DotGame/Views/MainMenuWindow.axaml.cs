using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace DotGameAvalonia.Views
{
    public partial class MainMenuWindow : Window
    {
        public MainMenuWindow()
        {
            InitializeComponent();
            AttachEvents();
        }

        private void AttachEvents()
        {
            var btnEditor = this.FindControl<Button>("BtnEditor");
            var btnSpriteEditor = this.FindControl<Button>("BtnSpriteEditor");
            var btnTestMap = this.FindControl<Button>("BtnTestMap");
            var btnCharCreate = this.FindControl<Button>("BtnCharCreate");

            if (btnEditor != null)
                btnEditor.Click += BtnEditor_Click;
            
            if (btnSpriteEditor != null)
                btnSpriteEditor.Click += BtnSpriteEditor_Click;
            
            if (btnTestMap != null)
                btnTestMap.Click += BtnTestMap_Click;
            
            if (btnCharCreate != null)
                btnCharCreate.Click += BtnCharCreate_Click;
        }

        private async void BtnEditor_Click(object? sender, RoutedEventArgs e)
        {
            var editor = new EditorWindow();
            await editor.ShowDialog(this);
        }

        private async void BtnSpriteEditor_Click(object? sender, RoutedEventArgs e)
        {
            var spriteEditor = new SpriteEditorWindow();
            await spriteEditor.ShowDialog(this);
        }

        private async void BtnTestMap_Click(object? sender, RoutedEventArgs e)
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null)
            {
                await new Window
                {
                    Content = new TextBlock { Text = "StorageProvider is not available." }
                }.ShowDialog(this);
                return;
            }

            var fileResult = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a Map File",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON Maps") { Patterns = new[] { "*.json" } }
                },
                AllowMultiple = false
            });

            if (fileResult != null && fileResult.Count > 0)
            {
                string selectedMap = fileResult[0].Path.LocalPath;

                if (File.Exists(selectedMap))
                {
                    var charDialog = new CharacterCreationWindow();
                    var dialogResult = await charDialog.ShowDialog<bool>(this);

                    if (dialogResult)
                    {
                        var sprite = charDialog.SelectedSprite;
                        var cls = charDialog.SelectedClass;
                        var name = string.IsNullOrWhiteSpace(charDialog.SelectedName) ? "Hero" : charDialog.SelectedName;

                        try
                        {
                            var game = new GameWindow(selectedMap, sprite, cls, name);
                            await game.ShowDialog(this);
                        }
                        catch (Exception ex)
                        {
                            await new Window
                            {
                                Content = new TextBlock { Text = $"Failed to launch game: {ex.Message}" }
                            }.ShowDialog(this);
                        }
                    }
                }
                else
                {
                    await new Window
                    {
                        Content = new TextBlock { Text = "Selected file does not exist." }
                    }.ShowDialog(this);
                }
            }
            else
            {
                await new Window
                {
                    Content = new TextBlock { Text = "No map file selected." }
                }.ShowDialog(this);
            }
        }

        private async void BtnCharCreate_Click(object? sender, RoutedEventArgs e)
        {
            var charDialog = new CharacterCreationWindow();
            await charDialog.ShowDialog(this);
        }
    }
}
