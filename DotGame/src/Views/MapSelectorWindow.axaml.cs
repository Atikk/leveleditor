using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DotGameAvalonia.Views
{
    public partial class MapSelectorWindow : Window
    {
        private TextBox? txtMapPath;
        private ListBox? lstMaps;

        public MapSelectorWindow()
        {
            InitializeComponent();
            AttachEvents();
            LoadAvailableMaps();
        }

        private void AttachEvents()
        {
            txtMapPath = this.FindControl<TextBox>("TxtMapPath");
            lstMaps = this.FindControl<ListBox>("LstMaps");
            
            var btnOk = this.FindControl<Button>("BtnOk");
            var btnCancel = this.FindControl<Button>("BtnCancel");

            if (btnOk != null)
                btnOk.Click += BtnOk_Click;
            
            if (btnCancel != null)
                btnCancel.Click += BtnCancel_Click;
        }

        private void LoadAvailableMaps()
        {
            if (lstMaps == null) return;

            var searchPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "maps"),
                "/home/runner/workspace/maps",
                "maps"
            };

            foreach (var dir in searchPaths)
            {
                if (Directory.Exists(dir))
                {
                    var jsonFiles = Directory.GetFiles(dir, "*.json");
                    foreach (var file in jsonFiles)
                    {
                        lstMaps.Items.Add($"{Path.GetFileName(file)} ({dir})");
                    }
                    if (jsonFiles.Length > 0) break;
                }
            }
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            string? selectedPath = null;

            if (lstMaps?.SelectedItem != null)
            {
                var selected = lstMaps.SelectedItem.ToString() ?? "";
                var fileName = selected.Split('(')[0].Trim();
                var dirPart = selected.Contains('(') ? selected.Split('(')[1].TrimEnd(')') : "/home/runner/workspace/maps";
                selectedPath = Path.Combine(dirPart, fileName);
            }
            else if (!string.IsNullOrWhiteSpace(txtMapPath?.Text))
            {
                var path = txtMapPath.Text;
                
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine("/home/runner/workspace", path);
                }
                selectedPath = path;
            }

            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
            {
                Close(selectedPath);
            }
            else
            {
                Close(null);
            }
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
