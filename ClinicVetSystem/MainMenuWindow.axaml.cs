using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class MainMenuWindow : Window
{
    private readonly Staff _loggedInStaff;

    public MainMenuWindow(Staff staff)
    {
        InitializeComponent();
        _loggedInStaff = staff;
        SetupMenuForRole();
    }

    private void SetupMenuForRole()
    {
        // Welcome message
        lblWelcome.Text = $"שלום, {_loggedInStaff.Username}!";
        lblRole.Text = _loggedInStaff.Role;

        // Show buttons based on role
        if (_loggedInStaff.Role == "מזכיר/ה")
        {
            btnCustomers.IsVisible = true;   // מזכיר/ה רואה ניהול לקוחות
            btnVisits.IsVisible = false;     // מזכיר/ה לא רואה ביקורים
        }
        else if (_loggedInStaff.Role == "וטרינר/ית")
        {
            btnCustomers.IsVisible = false;  // וטרינר לא רואה ניהול לקוחות
            btnVisits.IsVisible = true;      // וטרינר רואה ניהול ביקורים
        }
        // btnPets גלוי לשני התפקידים (IsVisible=True כברירת מחדל)
    }

    private void btnCustomers_Click(object sender, RoutedEventArgs e)
    {
        var customersWindow = new CustomersWindow();
        customersWindow.Show();
    }

    private void btnPets_Click(object sender, RoutedEventArgs e)
    {
        // TODO: פתח מסך ניהול חיות
    }

    private void btnVisits_Click(object sender, RoutedEventArgs e)
    {
        // TODO: פתח מסך ניהול ביקורים
    }

    private void btnLogout_Click(object sender, RoutedEventArgs e)
    {
        // חזרה למסך התחברות
        var loginWindow = new MainWindow();
        loginWindow.Show();
        this.Close();
    }
}
