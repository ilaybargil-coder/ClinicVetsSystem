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

    private void SetActiveTab(string tabName, bool clearFilters = true)
    {
        btnNavDashboard.Classes.Remove("active");
        btnNavCustomers.Classes.Remove("active");
        btnNavPets.Classes.Remove("active");

        DashboardPanel.IsVisible = false;
        CustomersPanel.IsVisible = false;
        PetsPanel.IsVisible = false;

        var mainContent = this.FindControl<ContentControl>("MainContentRegion");
        if (mainContent != null) mainContent.IsVisible = false;

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
                if (clearFilters) PetsPanel.ClearFilter();
                _ = PetsPanel.LoadDataAsync();
                break;
        }
    }

    private void NavigateTo(UserControl view, object sender)
    {
        btnNavDashboard.Classes.Remove("active");
        btnNavCustomers.Classes.Remove("active");
        btnNavPets.Classes.Remove("active");
        DashboardPanel.IsVisible = false;
        CustomersPanel.IsVisible = false;
        PetsPanel.IsVisible = false;

        if (_activeNavBtn != null) _activeNavBtn.Classes.Set("active", false);
        _activeNavBtn = sender as Button;
        _activeNavBtn?.Classes.Set("active", true);

        var mainContent = this.FindControl<ContentControl>("MainContentRegion");
        if (mainContent != null)
        {
            mainContent.Content = view;
            mainContent.IsVisible = true;
        }
    }

    private void btnNavDashboard_Click(object sender, RoutedEventArgs e) => SetActiveTab("Dashboard");
    private void btnNavCustomers_Click(object sender, RoutedEventArgs e) => SetActiveTab("Customers");
    private void btnPets_Click(object sender, RoutedEventArgs e) => SetActiveTab("Pets");
    private void BtnCalender_OnClick_Click(object? sender, RoutedEventArgs e) { }
    private void btnInventory_Click(object? sender, RoutedEventArgs e) => NavigateTo(new InventoryView(), sender!);

    private void btnVisits_Click(object? sender, RoutedEventArgs e)
    {
        if (_loggedInStaff.Role == "וטרינר/ית")
            NavigateTo(new VisitsView(_loggedInStaff), sender!);
    }

    public void GoToPetsForCustomer(string customerId)
    {
        SetActiveTab("Pets", clearFilters: false);
        PetsPanel.FilterByCustomer(customerId);
    }

    private void btnLogout_Click(object sender, RoutedEventArgs e)
    {
        new MainWindow().Show();
        this.Close();
    }
}
