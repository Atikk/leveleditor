using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DotGameAvalonia;

public partial class MainWindow : Window
{
    private int clickCount = 0;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        clickCount++;
        var resultText = this.FindControl<TextBlock>("ResultText");
        if (resultText != null)
        {
            resultText.Text = $"Button clicked {clickCount} time(s)! 🎉";
        }
    }
}
