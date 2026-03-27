using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClinicVetsSystem;
using ClinicVetsSystem.Models;

namespace ClinicVetSystem;

public partial class MainMenuWindow : Window
{
    private readonly Staff _loggedInStaff;

    public MainMenuWindow(Staff staff)
    {
        InitializeComponent();
        _loggedInStaff = staff;
        
        SetupMenuForRole();
        SetActiveTab("Dashboard"); // קביעת הטאב ההתחלתי שיוצג
    }

    // ─── Custom Window Chrome Logic ──────────────────────────────────────────

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();


    // ─── Role Authorization & SPA Routing ────────────────────────────────────

    private void SetupMenuForRole()
    {
        lblGreeting.Text = $"Morning, {_loggedInStaff.Username}";
        lblTopName.Text = _loggedInStaff.Username;
        lblTopRole.Text = _loggedInStaff.Role.ToUpper();

        // הסתרת הכפתורים הנכונים לפי תפקיד (השמות עודכנו ל-btnNav...)
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

    private void SetActiveTab(string tabName)
    {
        // 1. איפוס הסגנון של כל כפתורי הניווט (מסיר את המחלקה active)
        btnNavDashboard.Classes.Remove("active");
        btnNavCustomers.Classes.Remove("active");
        
        // 2. הסתרת כל הפאנלים (לוודא שכלום לא מוצג בטעות)
        DashboardPanel.IsVisible = false;
        CustomersPanel.IsVisible = false;

        // 3. הפעלת הפאנל והכפתור הרלוונטי בלבד
        switch (tabName)
        {
            case "Dashboard":
                btnNavDashboard.Classes.Add("active");
                DashboardPanel.IsVisible = true;
                break;
            case "Customers":
                btnNavCustomers.Classes.Add("active");
                CustomersPanel.IsVisible = true;
                // קריאה לרכיב הלקוחות שימשוך נתונים טריים מ-Supabase
                _ = CustomersPanel.LoadDataAsync(); 
                break;
        }
    }

    // אירועי לחיצה בסרגל הצד (שולחים לשם הטאב)
    private void btnNavDashboard_Click(object sender, RoutedEventArgs e) => SetActiveTab("Dashboard");
    private void btnNavCustomers_Click(object sender, RoutedEventArgs e) => SetActiveTab("Customers");
    
    // אלה יפותחו בהמשך כשניצור להם UserControls
    private void btnPets_Click(object sender, RoutedEventArgs e) { /* SetActiveTab("Pets"); */ }
    private void btnVisits_Click(object sender, RoutedEventArgs e) { /* SetActiveTab("Visits"); */ }

    // התנתקות
    private void btnLogout_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new MainWindow();
        loginWindow.Show();
        this.Close();
    }
}