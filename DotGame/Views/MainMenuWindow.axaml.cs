using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

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
            string selectedMap = "/home/runner/workspace/maps/simple.json";
            
            if (File.Exists(selectedMap))
            {
                var charDialog = new CharacterCreationWindow();
                var dialogResult = await charDialog.ShowDialog<bool>(this);
                
                if (dialogResult)
                {
                    var sprite = charDialog.SelectedSprite;
                    var cls = charDialog.SelectedClass;
                    var name = string.IsNullOrWhiteSpace(charDialog.SelectedName) ? "Hero" : charDialog.SelectedName;
                    
                    var game = new GameWindow(selectedMap, sprite, cls, name);
                    await game.ShowDialog(this);
                }
            }
        }

        private async void BtnCharCreate_Click(object? sender, RoutedEventArgs e)
        {
            var charDialog = new CharacterCreationWindow();
            await charDialog.ShowDialog(this);
        }
    }
}
