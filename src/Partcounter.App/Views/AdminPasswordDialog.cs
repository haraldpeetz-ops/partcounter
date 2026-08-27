using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Partcounter.Services;

namespace Partcounter.Views;

public enum AdminPasswordDialogMode
{
    Unlock,
    Setup,
    Change
}

public sealed class AdminPasswordDialog : Window
{
    private readonly AdminPasswordDialogMode _mode;
    private readonly PasswordBox _passwordBox = new();
    private readonly PasswordBox _confirmBox = new();
    private readonly TextBlock _errorText = new();

    public AdminPasswordDialog(AdminPasswordDialogMode mode)
    {
        _mode = mode;
        Title = mode switch
        {
            AdminPasswordDialogMode.Unlock => "Administration entsperren",
            AdminPasswordDialogMode.Setup => "Admin-Passwort einrichten",
            _ => "Admin-Passwort ändern"
        };

        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7));
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(22) };
        Content = root;

        root.Children.Add(new TextBlock
        {
            Text = Title,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x21, 0x2B)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        root.Children.Add(new TextBlock
        {
            Text = mode == AdminPasswordDialogMode.Unlock
                ? "Die gewählte Funktion verändert System-, LOGO-, Modbus-, Etiketten- oder Druckeinstellungen und ist deshalb geschützt."
                : $"Das Passwort schützt alle administrativen Bereiche. Mindestens {AdminAccessService.MinimumPasswordLength} Zeichen verwenden.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x64, 0x73)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        root.Children.Add(new TextBlock { Text = "Admin-Passwort", FontWeight = FontWeights.SemiBold });
        _passwordBox.MinHeight = 30;
        _passwordBox.Margin = new Thickness(0, 4, 0, 10);
        root.Children.Add(_passwordBox);

        if (mode is AdminPasswordDialogMode.Setup or AdminPasswordDialogMode.Change)
        {
            root.Children.Add(new TextBlock { Text = "Passwort wiederholen", FontWeight = FontWeights.SemiBold });
            _confirmBox.MinHeight = 30;
            _confirmBox.Margin = new Thickness(0, 4, 0, 8);
            root.Children.Add(_confirmBox);
        }

        _errorText.Foreground = Brushes.Firebrick;
        _errorText.TextWrapping = TextWrapping.Wrap;
        _errorText.Margin = new Thickness(0, 2, 0, 10);
        root.Children.Add(_errorText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancel = new Button { Content = "Abbrechen", MinWidth = 95, Margin = new Thickness(4), Padding = new Thickness(10, 6, 10, 6) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(cancel);

        var ok = new Button
        {
            Content = mode == AdminPasswordDialogMode.Unlock ? "Entsperren" : "Speichern",
            MinWidth = 105,
            Margin = new Thickness(4),
            Padding = new Thickness(10, 6, 10, 6),
            FontWeight = FontWeights.SemiBold,
            IsDefault = true
        };
        ok.Click += OnConfirm;
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        Loaded += (_, _) => _passwordBox.Focus();
    }

    public string Password => _passwordBox.Password;

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        _errorText.Text = string.Empty;

        if (_mode == AdminPasswordDialogMode.Unlock)
        {
            if (string.IsNullOrEmpty(_passwordBox.Password))
            {
                _errorText.Text = "Bitte Admin-Passwort eingeben.";
                return;
            }
        }
        else
        {
            try
            {
                AdminAccessService.ValidatePassword(_passwordBox.Password);
            }
            catch (Exception ex)
            {
                _errorText.Text = ex.Message;
                return;
            }

            if (!string.Equals(_passwordBox.Password, _confirmBox.Password, StringComparison.Ordinal))
            {
                _errorText.Text = "Die beiden Passwörter stimmen nicht überein.";
                return;
            }
        }

        DialogResult = true;
        Close();
    }
}
