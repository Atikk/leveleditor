using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using DotGameAvalonia.Models;
using DotGameAvalonia.Assets;

namespace DotGameAvalonia.Views
{
    public partial class CharacterCreationWindow : Window
    {
        public Bitmap? SelectedSprite { get; private set; }
        public CharacterClass SelectedClass { get; private set; } = CharacterClass.Warrior;
        public string? SelectedName { get; private set; }

        private Image? imgPreview;
        private TextBox? txtName;
        private TextBox? txtSpritePath;
        private ComboBox? cmbClass;
        private TextBlock? lblStats;
        private TextBlock? lblClassDesc;

        public CharacterCreationWindow()
        {
            InitializeComponent();
            AttachEvents();
            // provide a default sprite for easier testing
            try
            {
                SelectedSprite = DefaultSprite.GetDefaultSprite();
                imgPreview = this.FindControl<Image>("ImgPreview");
                if (imgPreview != null)
                    imgPreview.Source = SelectedSprite;
            }
            catch
            {
                // ignore failures generating default sprite
            }
            UpdateStats();
        }

        private void AttachEvents()
        {
            imgPreview = this.FindControl<Image>("ImgPreview");
            txtName = this.FindControl<TextBox>("TxtName");
            txtSpritePath = this.FindControl<TextBox>("TxtSpritePath");
            cmbClass = this.FindControl<ComboBox>("CmbClass");
            lblStats = this.FindControl<TextBlock>("LblStats");
            lblClassDesc = this.FindControl<TextBlock>("LblClassDesc");
            
            var btnLoadSprite = this.FindControl<Button>("BtnLoadSprite");
            var btnOk = this.FindControl<Button>("BtnOk");
            var btnCancel = this.FindControl<Button>("BtnCancel");
            var btnSaveUnit = this.FindControl<Button>("BtnSaveUnit");
            var btnAddToMap = this.FindControl<Button>("BtnAddToMap");

            if (btnLoadSprite != null)
                btnLoadSprite.Click += BtnLoadSprite_Click;
            
            if (btnOk != null)
                btnOk.Click += BtnOk_Click;
            
            if (btnCancel != null)
                btnCancel.Click += BtnCancel_Click;
            
            if (cmbClass != null)
                cmbClass.SelectionChanged += CmbClass_SelectionChanged;

            if (btnSaveUnit != null)
                btnSaveUnit.Click += BtnSaveUnit_Click;

            if (btnAddToMap != null)
                btnAddToMap.Click += BtnAddToMap_Click;
        }

        private void BtnLoadSprite_Click(object? sender, RoutedEventArgs e)
        {
            if (txtSpritePath != null && !string.IsNullOrWhiteSpace(txtSpritePath.Text))
            {
                var path = txtSpritePath.Text;

                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine("/home/runner/workspace", path);
                }

                if (File.Exists(path))
                {
                    try
                    {
                        SelectedSprite = new Bitmap(path);
                        if (imgPreview != null)
                            imgPreview.Source = SelectedSprite;
                    }
                    catch (Exception ex)
                    {
                        SelectedSprite = null;
                        if (imgPreview != null)
                            imgPreview.Source = null;

                        // Display error message for unsupported formats or loading issues
                        var errorWindow = new Window
                        {
                            Content = new TextBlock { Text = $"Failed to load sprite: {ex.Message}" },
                            Width = 300,
                            Height = 100
                        };
                        errorWindow.ShowDialog(this);
                    }
                }
                else
                {
                    // Display error message for missing file
                    var errorWindow = new Window
                    {
                        Content = new TextBlock { Text = "Sprite file does not exist." },
                        Width = 300,
                        Height = 100
                    };
                    errorWindow.ShowDialog(this);
                }
            }
            else
            {
                // Display error message for empty path
                var errorWindow = new Window
                {
                    Content = new TextBlock { Text = "Please enter a valid sprite path." },
                    Width = 300,
                    Height = 100
                };
                errorWindow.ShowDialog(this);
            }
        }

        private void CmbClass_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (cmbClass != null && lblStats != null)
            {
                SelectedClass = (CharacterClass)cmbClass.SelectedIndex;
                var stats = Character.GetBaseStats(SelectedClass);
                lblStats.Text = $"HP: {stats.MaxHP} | Attack: {stats.Attack} | Defense: {stats.Defense}";

                if (lblClassDesc != null)
                {
                    lblClassDesc.Text = SelectedClass switch
                    {
                        CharacterClass.Warrior => "⚔️ A strong fighter with high HP and Defense. Excels in close combat.",
                        CharacterClass.Mage => "🔮 A powerful spellcaster with high Attack but low Defense. Deals massive damage.",
                        CharacterClass.Thief => "🗡️ A balanced rogue with moderate stats. Quick and versatile in battle.",
                        _ => ""
                    };
                }
            }
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            if (cmbClass != null)
                SelectedClass = (CharacterClass)cmbClass.SelectedIndex;
            
            if (txtName != null)
                SelectedName = txtName.Text;
            
            Close(true);
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            SelectedSprite = null;
            Close(false);
        }

        private void BtnSaveUnit_Click(object? sender, RoutedEventArgs e)
        {
            if (SelectedName != null && SelectedSprite != null)
            {
                var unit = new Character(0, 0, SelectedClass, SelectedName)
                {
                    Sprite = SelectedSprite
                };

                // Save unit to file or database (pseudo-code)
                UnitRepository.Save(unit);
                Console.WriteLine("Unit saved successfully.");
            }
            else
            {
                Console.WriteLine("Cannot save unit. Ensure name and sprite are selected.");
            }
        }

        private void BtnAddToMap_Click(object? sender, RoutedEventArgs e)
        {
            if (SelectedName != null && SelectedSprite != null)
            {
                var unit = new Character(0, 0, SelectedClass, SelectedName)
                {
                    Sprite = SelectedSprite
                };

                // Add unit to map (pseudo-code)
                MapEditor.AddUnitToMap(unit);
                Console.WriteLine("Unit added to map successfully.");
            }
            else
            {
                Console.WriteLine("Cannot add unit to map. Ensure name and sprite are selected.");
            }
        }
    }
}
