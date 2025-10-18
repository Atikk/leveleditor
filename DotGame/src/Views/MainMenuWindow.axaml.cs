using System;
using System.IO;
using System.Linq;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Platform.Storage;
using Dotgame.Avalonia.Models;

namespace Dotgame.Avalonia.Views
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
            var btnAnimationEditor = this.FindControl<Button>("BtnAnimationEditor");

            if (btnEditor != null)
                btnEditor.Click += BtnEditor_Click;
            
            if (btnSpriteEditor != null)
                btnSpriteEditor.Click += BtnSpriteEditor_Click;

            if (btnAnimationEditor != null)
                btnAnimationEditor.Click += BtnAnimationEditor_Click;
            
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

        private async void BtnAnimationEditor_Click(object? sender, RoutedEventArgs e)
        {
            var animationEditor = new AnimationEditorWindow();
            await animationEditor.ShowDialog(this);
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
                    try
                    {
                        // Validate the map file
                        var map = Map.LoadFromJson(selectedMap);
                        if (map == null)
                        {
                            await new Window
                            {
                                Content = new TextBlock { Text = "The selected file does not contain valid map data." }
                            }.ShowDialog(this);
                            return;
                        }

                        var game = new GameWindow(selectedMap, null, CharacterClass.Warrior, "Hero");
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


