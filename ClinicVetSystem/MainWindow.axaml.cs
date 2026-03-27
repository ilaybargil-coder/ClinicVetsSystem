using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ClinicVetsSystem.Models;
using System;
using System.Linq;

namespace ClinicVetsSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void btnLogin_Click(object sender, RoutedEventArgs e)
    {
        string username = txtUsername.Text?.Trim() ?? "";
        string password = txtPassword.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            lblMessage.Foreground = Brushes.Red;
            lblMessage.Text = "נא למלא שם משתמש וסיסמה.";
            lblMessage.IsVisible = true;
            return;
        }

        // Disable button during login
        btnLogin.IsEnabled = false;
        lblMessage.IsVisible = false;

        try
        {
            var client = SupabaseService.Client;
            if (client == null)
            {
                lblMessage.Foreground = Brushes.Red;
                lblMessage.Text = "שגיאה: אין חיבור לשרת.";
                lblMessage.IsVisible = true;
                return;
            }

            // Query the staff table for matching username + password
            var result = await client
                .From<Staff>()
                .Where(s => s.Username == username && s.Password == password)
                .Get();

            var staffMember = result.Models.FirstOrDefault();

            if (staffMember != null)
            {
                // פתח את התפריט הראשי וסגור את חלון הלוגין
                var mainMenu = new MainMenuWindow(staffMember);
                mainMenu.Show();
                this.Close();
            }
            else
            {
                lblMessage.Foreground = Brushes.Red;
                lblMessage.Text = "שם משתמש או סיסמה שגויים.";
                lblMessage.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            lblMessage.Foreground = Brushes.Red;
            lblMessage.Text = $"שגיאת חיבור: {ex.Message}";
            lblMessage.IsVisible = true;
        }
        finally
        {
            btnLogin.IsEnabled = true;
        }
    }

    private void btnRegister_Click(object sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow();
        registerWindow.Show();
    }
}
