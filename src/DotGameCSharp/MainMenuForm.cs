using System;
using System.Windows.Forms;

namespace DotGameCSharp
{
    public partial class MainMenuForm : Form
    {
        public MainMenuForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DotGame – Main Menu";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.ClientSize = new System.Drawing.Size(300, 200);

            var btnEditor   = new Button { Text = "Map Editor",   Dock = DockStyle.Top, Height = 40 };
            var btnTestMap  = new Button { Text = "Test Map",    Dock = DockStyle.Top, Height = 40 };
            var btnCharCreate = new Button { Text = "Create Character", Dock = DockStyle.Top, Height = 40 };

            btnEditor.Click += (s, e) =>
            {
                using var editor = new EditorForm();
                editor.ShowDialog();
            };

            btnTestMap.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "JSON Maps|*.json" };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    using var charSel = new CharacterCreationForm();
                    if (charSel.ShowDialog() == DialogResult.OK)
                    {
                        var sprite = charSel.SelectedSprite;
                        var cls = charSel.SelectedClass;
                        var name = string.IsNullOrWhiteSpace(charSel.SelectedName) ? "Hero" : charSel.SelectedName;
                        using var game = new GameForm(ofd.FileName, sprite, cls, name);
                        game.ShowDialog();
                    }
                }
            };

            btnCharCreate.Click += (s, e) =>
            {
                using var charSel = new CharacterCreationForm();
                charSel.ShowDialog();
            };

            Controls.Add(btnCharCreate);
            Controls.Add(btnTestMap);
            Controls.Add(btnEditor);
        }
    }
}
