using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using DotGameAvalonia.Models;

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

            if (btnLoadSprite != null)
                btnLoadSprite.Click += BtnLoadSprite_Click;
            
            if (btnOk != null)
                btnOk.Click += BtnOk_Click;
            
            if (btnCancel != null)
                btnCancel.Click += BtnCancel_Click;
            
            if (cmbClass != null)
                cmbClass.SelectionChanged += CmbClass_SelectionChanged;
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
                    catch
                    {
                        SelectedSprite = null;
                        if (imgPreview != null)
                            imgPreview.Source = null;
                    }
                }
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
    }
}
