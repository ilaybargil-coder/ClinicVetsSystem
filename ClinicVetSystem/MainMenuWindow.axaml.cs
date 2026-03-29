using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class MainMenuWindow : Window
{
    private readonly Staff _loggedInStaff;
    private Button? _activeNavBtn;

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

        // אתחול כפתור פעיל
        _activeNavBtn = this.FindControl<Button>("btnNavDashboard");

        // בדיקה אילו כפתורים להציג
        var btnVisits = this.FindControl<Button>("btnNavVisits");
        if (btnVisits != null)
            btnVisits.IsVisible = (_loggedInStaff.Role == "וטרינר/ית");
    }

    // --- ניווט ---
    private void SetActiveNav(object sender)
    {
        if (_activeNavBtn != null)
            _activeNavBtn.Classes.Set("active", false);
        _activeNavBtn = sender as Button;
        _activeNavBtn?.Classes.Set("active", true);
    }

    private void NavigateTo(UserControl view, object sender)
    {
        SetActiveNav(sender);
        var mainContent = this.FindControl<ContentControl>("MainContentRegion");
        var dashboard = this.FindControl<ScrollViewer>("DashboardPanel");
        if (mainContent != null && dashboard != null)
        {
            mainContent.Content = view;
            mainContent.IsVisible = true;
            dashboard.IsVisible = false;
        }
    }

    private void btnNavDashboard_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(sender);
        var mainContent = this.FindControl<ContentControl>("MainContentRegion");
        var dashboard = this.FindControl<ScrollViewer>("DashboardPanel");
        if (mainContent != null && dashboard != null)
        {
            mainContent.IsVisible = false;
            dashboard.IsVisible = true;
        }
    }

    private void btnNavCustomers_Click(object sender, RoutedEventArgs e) { }

    private void btnPets_Click(object sender, RoutedEventArgs e) { }

    private void BtnCalender_OnClick_Click(object sender, RoutedEventArgs e) { }

    private void btnInventory_Click(object? sender, RoutedEventArgs e) => NavigateTo(new InventoryView(), sender!);

    private void btnVisits_Click(object? sender, RoutedEventArgs e)
    {
        if (_loggedInStaff.Role == "וטרינר/ית")
            NavigateTo(new VisitsView(_loggedInStaff), sender!);
    }

    private void btnLogout_Click(object sender, RoutedEventArgs e)
    {
        new MainWindow().Show();
        this.Close();
    }
}