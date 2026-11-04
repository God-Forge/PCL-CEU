using System.Windows;

namespace PCL.App.Controls;

public partial class InputDialog : Window {

    public InputDialog() {
        InitializeComponent();
    }

    public static string? Show(string title, string prompt, string initialValue = "") {
        var dialog = new InputDialog {
            Title = title,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        dialog.PromptText.Text = prompt;
        dialog.InputBox.Text = initialValue;
        dialog.InputBox.SelectAll();
        dialog.InputBox.Focus();
        MeloongCore.Logger.Info("[InputDialog] before ShowDialog");
        var result = dialog.ShowDialog();
        MeloongCore.Logger.Info($"[InputDialog] after ShowDialog result={result}, visible={dialog.IsVisible}, dialogResult={dialog.DialogResult}");
        return result == true ? dialog.InputBox.Text : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) {
        DialogResult = true;
    }

}
