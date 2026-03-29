using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;
using System;

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

    // --- כפתורי כותרת ---
    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // --- הגדרת תפריט לפי תפקיד ---
    private void SetupMenuForRole()
    {
        // וודאי שהשמות האלו קיימים ב-XAML שלך (למשל x:Name="lblGreeting")
        if (this.FindControl<TextBlock>("lblGreeting") != null)
            this.FindControl<TextBlock>("lblGreeting").Text = $"שלום, {_loggedInStaff.Username}";

        // בדיקה אילו כפתורים להציג
        var btnVisits = this.FindControl<Button>("btnNavVisits");
        if (btnVisits != null)
            btnVisits.IsVisible = (_loggedInStaff.Role == "וטרינר/ית");
    }

    // --- ניווט ---
    private void btnNavDashboard_Click(object sender, RoutedEventArgs e) 
    {
        // כאן את יכולה להחזיר את התוכן של ה-Dashboard
    }

    private void btnInventory_Click(object? sender, RoutedEventArgs e)
    {
        var mainContent = this.FindControl<ContentControl>("MainContentRegion") ?? this.FindControl<ContentControl>("DashboardPanel");
        if (mainContent != null)
            mainContent.Content = new InventoryView();
    }

    private void btnVisits_Click(object? sender, RoutedEventArgs e)
    {
        if (_loggedInStaff.Role == "וטרינר/ית")
        {
            var mainContent = this.FindControl<ContentControl>("MainContentRegion") ?? this.FindControl<ContentControl>("DashboardPanel");
            if (mainContent != null)
                mainContent.Content = new VisitsView(_loggedInStaff);
        }
    }

    private void btnLogout_Click(object sender, RoutedEventArgs e)
    {
        new MainWindow().Show();
        this.Close();
    }
}