using Avalonia.Controls;
using Avalonia.Input;
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
        SetActiveTab("Dashboard"); 
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SetupMenuForRole()
    {
        lblGreeting.Text = $"Morning, {_loggedInStaff.Username}";
        lblTopName.Text = _loggedInStaff.Username;
        lblTopRole.Text = _loggedInStaff.Role.ToUpper();

        if (_loggedInStaff.Role == "מזכיר/ה")
        {
            btnNavCustomers.IsVisible = true;
            btnNavVisits.IsVisible = false;
        }
        else if (_loggedInStaff.Role == "וטרינר/ית")
        {
            btnNavCustomers.IsVisible = false;
            btnNavVisits.IsVisible = true;
        }
    }

    // הוספנו כאן את המשתנה clearFilters (ברירת מחדל: true)
    private void SetActiveTab(string tabName, bool clearFilters = true)
    {
        btnNavDashboard.Classes.Remove("active");
        btnNavCustomers.Classes.Remove("active");
        btnNavPets.Classes.Remove("active");

        DashboardPanel.IsVisible = false;
        CustomersPanel.IsVisible = false;
        PetsPanel.IsVisible = false;

        switch (tabName)
        {
            case "Dashboard":
                btnNavDashboard.Classes.Add("active");
                DashboardPanel.IsVisible = true;
                break;
            case "Customers":
                btnNavCustomers.Classes.Add("active");
                CustomersPanel.IsVisible = true;
                _ = CustomersPanel.LoadDataAsync();
                break;
            case "Pets":
                btnNavPets.Classes.Add("active");
                PetsPanel.IsVisible = true;
                
                // אם הגענו מהתפריט הרגיל, ננקה את הסינון כדי לראות את כל החיות
                if (clearFilters)
                {
                    PetsPanel.ClearFilter();
                }
                
                _ = PetsPanel.LoadDataAsync();
                break;
        }
    }

    private void btnNavDashboard_Click(object sender, RoutedEventArgs e) => SetActiveTab("Dashboard");
    private void btnNavCustomers_Click(object sender, RoutedEventArgs e) => SetActiveTab("Customers");
    
    // לחיצה רגילה בתפריט - מנקה את הסינון!
    private void btnPets_Click(object sender, RoutedEventArgs e) => SetActiveTab("Pets"); 
    
    private void btnVisits_Click(object sender, RoutedEventArgs e) { /* יפותח בהמשך */ }
    private void BtnCalender_OnClick_Click(object? sender, RoutedEventArgs e) { /* יפותח בהמשך */ }

    // הלוגיקה שתוקנה עבור באג 1! מעבירה לטאב בלי לנקות את הסינון, ומפעילה את הסינון עצמו
    public void GoToPetsForCustomer(string customerId)
    {
        SetActiveTab("Pets", clearFilters: false);
        PetsPanel.FilterByCustomer(customerId);
    }

    private void btnLogout_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new MainWindow();
        loginWindow.Show();
        this.Close();
    }
}