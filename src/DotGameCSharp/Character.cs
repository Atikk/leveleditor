using System;
using System.Drawing;
using System.Windows.Forms;

namespace DotGameCSharp
{
    public enum Facing { Down, Left, Right, Up }
    public enum CharacterClass { Warrior, Mage, Thief }

    public struct Stats
    {
        public int MaxHP;
        public int Attack;
        public int Defense;
    }

    public enum AnimationState { Idle, Walk }

    public sealed class Character
    {
        public int TileX { get; private set; }
        public int TileY { get; private set; }

        public Color Color { get; set; } = Color.DeepSkyBlue;

        public Bitmap? Sprite { get; set; }

        public int FrameWidth { get; private set; } = 32;
        public int FrameHeight { get; private set; } = 32;
        public int TotalFrames { get; private set; } = 1;

        public Facing Direction { get; private set; } = Facing.Down;
        public int FrameIndex { get; private set; } = 0;

        public CharacterClass Class { get; private set; } = CharacterClass.Warrior;

        public Stats Attributes { get; private set; }
        
        public string Name { get; private set; } = "Hero";

        public int AnimationDelay { get; set; } = 5;

        private int animationCounter = 0;

        public Character(int tileX, int tileY)
        {
            TileX = tileX;
            TileY = tileY;
            Class = CharacterClass.Warrior;
            Name = "Hero";
            Attributes = GetBaseStats(Class);
        }

        public Character(int tileX, int tileY, CharacterClass cls, string name)
            : this(tileX, tileY)
        {
            Class = cls;
            Name = name;
            Attributes = GetBaseStats(cls);
        }

        public static Stats GetBaseStats(CharacterClass cls)
        {
            return cls switch
            {
                CharacterClass.Warrior => new Stats { MaxHP = 30, Attack = 5, Defense = 5 },
                CharacterClass.Mage    => new Stats { MaxHP = 20, Attack = 7, Defense = 3 },
                CharacterClass.Thief   => new Stats { MaxHP = 25, Attack = 6, Defense = 4 },
                _ => new Stats { MaxHP = 10, Attack = 3, Defense = 3 },
            };
        }

        public void LoadSprite(string path, int frameW = 32, int frameH = 32, int totalFrames = 1)
        {
            Sprite = new Bitmap(path);
            FrameWidth = frameW;
            FrameHeight = frameH;
            TotalFrames = Math.Max(1, totalFrames);
        }

        public void Draw(Graphics g, Map map)
        {
            var rect = map.TileRect(TileX, TileY);

            if (Sprite != null)
            {
                var src = new Rectangle(FrameIndex * FrameWidth, (int)Direction * FrameHeight, FrameWidth, FrameHeight);
                g.DrawImage(Sprite, rect, src, GraphicsUnit.Pixel);
            }
            else
            {
                using var b = new SolidBrush(Color);
                using var p = new Pen(Color.Black, 2);
                g.FillRectangle(b, rect);
                g.DrawRectangle(p, rect);
            }
        }

        public void TryMove(int dx, int dy, Map map)
        {
            int nx = TileX + dx;
            int ny = TileY + dy;
            if (map.InBounds(nx, ny))
            {
                TileX = nx;
                TileY = ny;
                UpdateDirection(dx, dy);
                AdvanceFrame();
            }
        }

        private void UpdateDirection(int dx, int dy)
        {
            if (dy < 0) Direction = Facing.Up;
            else if (dy > 0) Direction = Facing.Down;
            else if (dx < 0) Direction = Facing.Left;
            else if (dx > 0) Direction = Facing.Right;
        }

        private void AdvanceFrame()
        {
            if (TotalFrames > 1)
                FrameIndex = (FrameIndex + 1) % TotalFrames;
        }

        public void UpdateAnimation()
        {
            if (TotalFrames > 1)
            {
                animationCounter++;
                if (animationCounter >= AnimationDelay)
                {
                    AdvanceFrame();
                    animationCounter = 0;
                }
            }
        }
    }
   
    public partial class CharacterCreationForm : Form
    {
        public Bitmap? SelectedSprite { get; private set; }
        public CharacterClass SelectedClass { get; private set; } = CharacterClass.Warrior;
        public string? SelectedName { get; private set; }

        public CharacterCreationForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Create or Choose Character";
            Width = 350;
            Height = 400;
            StartPosition = FormStartPosition.CenterParent;

            var btnLoad = new Button { Text = "Load Image...", Dock = DockStyle.Top, Height = 30 };
            var lblPreview = new PictureBox { Dock = DockStyle.Top, Height = 120, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };

            var lblName = new Label { Text = "Name:", Dock = DockStyle.Top, Height = 20 };
            var txtName = new TextBox { Dock = DockStyle.Top, Height = 20 };

            var lblClass = new Label { Text = "Class:", Dock = DockStyle.Top, Height = 20 };
            var cmbClass = new ComboBox { Dock = DockStyle.Top, Height = 20, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbClass.Items.AddRange(Enum.GetNames(typeof(CharacterClass)));
            cmbClass.SelectedIndex = 0;

            var lblStats = new Label { Text = "", Dock = DockStyle.Top, Height = 60, TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.FixedSingle };

            var btnOk = new Button { Text = "OK", Dock = DockStyle.Bottom, Height = 30 };
            var btnCancel = new Button { Text = "Cancel", Dock = DockStyle.Bottom, Height = 30 };

            btnLoad.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    SelectedSprite = new Bitmap(ofd.FileName);
                    lblPreview.Image = SelectedSprite;
                }
            };

            EventHandler updateStats = (s, e) =>
            {
                SelectedClass = (CharacterClass)cmbClass.SelectedIndex;
                var stats = Character.GetBaseStats(SelectedClass);
                lblStats.Text = $"HP: {stats.MaxHP}\nAttack: {stats.Attack}\nDefense: {stats.Defense}";
            };
            cmbClass.SelectedIndexChanged += updateStats;
            updateStats(null!, EventArgs.Empty);

            btnOk.Click += (s, e) =>
            {
                SelectedClass = (CharacterClass)cmbClass.SelectedIndex;
                SelectedName = txtName.Text;
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                SelectedSprite = null;
                Close();
            };

            Controls.Add(btnLoad);
            Controls.Add(lblPreview);
            Controls.Add(lblName);
            Controls.Add(txtName);
            Controls.Add(lblClass);
            Controls.Add(cmbClass);
            Controls.Add(lblStats);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }
}
