using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DotGameAvalonia.Views
{
    public partial class EditorWindow : Window
    {
        public EditorWindow()
        {
            InitializeComponent();
            AttachEvents();
        }

        private void AttachEvents()
        {
            var btnClose = this.FindControl<Button>("BtnClose");
            if (btnClose != null)
                btnClose.Click += (s, e) => Close();
        }
    }
}
