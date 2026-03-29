using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ClinicVetsSystem.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClinicVetsSystem;

public partial class MainWindow : Window
{
    private bool _isLoginView = true;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ─── Custom Window Chrome & Sliding Logic ────────────────────────────────

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // בלחיצה על כפתור ההחלפה - מריצים את הסלייד אפקט!
    private void ToggleSlide_Click(object sender, RoutedEventArgs e)
    {
        _isLoginView = !_isLoginView;
        UpdateSlidePosition();
    }

    // אם המסך משנה גודל (Resize), אנחנו מתקנים את המיקום כדי שהאנימציה תישאר חלקה
    private void ContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSlidePosition();
    }

    private void UpdateSlidePosition()
    {
        // הפאנל הירוק מחליק את הרוחב של העמודה השמאלית כשהוא עובר ימינה
        double targetX = _isLoginView ? 0 : LeftPanel.Bounds.Width;
        
        // ביצוע האנימציה (Web3 Style TranslateX)
        OverlayPanel.RenderTransform = TransformOperations.Parse($"translateX({targetX}px)");

        // הפעלת מעברי שקיפות לטקסטים
        if (_isLoginView)
        {
            OverlayLeftContent.Opacity = 1;
            OverlayLeftContent.IsHitTestVisible = true;
            
            OverlayRightContent.Opacity = 0;
            OverlayRightContent.IsHitTestVisible = false;
        }
        else
        {
            OverlayLeftContent.Opacity = 0;
            OverlayLeftContent.IsHitTestVisible = false;
            
            OverlayRightContent.Opacity = 1;
            OverlayRightContent.IsHitTestVisible = true;
        }
    }


    // ─── Login Logic ─────────────────────────────────────────────────────────

    private async void btnLoginSubmit_Click(object sender, RoutedEventArgs e)
    {
        string email = txtLoginEmail.Text?.Trim() ?? "";
        string password = txtLoginPassword.Text ?? "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowLoginError("נא למלא אימייל וסיסמה.");
            return;
        }

        btnLoginSubmit.IsEnabled = false;
        lblLoginMessage.IsVisible = false;

        try
        {
            var client = SupabaseService.Client;
            if (client == null)
            {
                ShowLoginError("שגיאה: אין חיבור לשרת.");
                return;
            }

            var result = await client
                .From<Staff>()
                .Where(s => s.Username == email && s.Password == password) // בהנחה שהלוגין הוא לפי אימייל (לפי העיצוב)
                .Get();

            var staffMember = result.Models.FirstOrDefault();

            if (staffMember != null)
            {
                var mainMenu = new MainMenuWindow(staffMember);
                mainMenu.Show();
                this.Close();
            }
            else
            {
                ShowLoginError("אימייל או סיסמה שגויים.");
            }
        }
        catch (Exception ex)
        {
            ShowLoginError($"שגיאת חיבור: {ex.Message}");
        }
        finally
        {
            btnLoginSubmit.IsEnabled = true;
        }
    }

    private void ShowLoginError(string msg)
    {
        lblLoginMessage.Text = msg;
        lblLoginMessage.IsVisible = true;
    }


    // ─── Registration Logic ──────────────────────────────────────────────────

    private async void btnSubmitRegister_Click(object sender, RoutedEventArgs e)
    {
        string username = txtRegUsername.Text?.Trim() ?? "";
        string password = txtRegPassword.Text ?? "";
        string employeeNum = txtRegEmpNum.Text?.Trim() ?? "";
        string id = txtRegId.Text?.Trim() ?? "";
        string email = txtRegEmail.Text?.Trim() ?? "";
        string role = (cmbRegRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        bool isValid = true;

        if (!ValidateRegex(username, @"^[a-zA-Z0-9]{6,8}$") || username.Count(char.IsDigit) > 2)
        { ShowRegError(lblUsernameError, "שם משתמש: 6-8 תווים, מקס' 2 ספרות."); isValid = false; }
        else HideRegError(lblUsernameError);

        bool passOk = password.Length >= 8 && password.Length <= 10 && password.Any(char.IsLetter) && password.Count(char.IsDigit) == 1 && password.Count(c => "$#!.".Contains(c)) == 1;
        if (!passOk)
        { ShowRegError(lblPasswordError, "סיסמה: 8-10 תווים, אות, ספרה אחת, תו מיוחד ($#!.)."); isValid = false; }
        else HideRegError(lblPasswordError);

        if (!ValidateRegex(employeeNum, @"^\d{4}$"))
        { ShowRegError(lblEmpNumError, "מספר עובד: בדיוק 4 ספרות."); isValid = false; }
        else HideRegError(lblEmpNumError);

        if (!ValidateRegex(id, @"^\d{9}$"))
        { ShowRegError(lblIdError, "ת\"ז: בדיוק 9 ספרות."); isValid = false; }
        else HideRegError(lblIdError);

        if (!ValidateRegex(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        { ShowRegError(lblEmailError, "אימייל אינו תקין."); isValid = false; }
        else HideRegError(lblEmailError);

        if (string.IsNullOrEmpty(role))
        { ShowRegError(lblRoleError, "יש לבחור תפקיד."); isValid = false; }
        else HideRegError(lblRoleError);

        if (!isValid) return;

        btnSubmitRegister.IsEnabled = false;
        lblRegMessage.IsVisible = false;

        try
        {
            var newStaff = new Staff
            {
                Username = username,
                Password = password,
                EmployeeNumber = int.Parse(employeeNum),
                Email = email,
                Role = role
            };

            await SupabaseService.Client!.From<Staff>().Insert(newStaff);

            // Success Message
            lblRegMessage.Foreground = SolidColorBrush.Parse("#006c49");
            lblRegMessage.Background = SolidColorBrush.Parse("#d1fae5");
            lblRegMessage.Text = "✓ Registration Successful! Please switch to Login.";
            lblRegMessage.IsVisible = true;

            // Optional: Auto switch to login after 2 seconds
            // await System.Threading.Tasks.Task.Delay(2000);
            // ToggleSlide_Click(null, null);
        }
        catch (Exception ex)
        {
            lblRegMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
            lblRegMessage.Background = SolidColorBrush.Parse("#1Affdad6");
            lblRegMessage.Text = ex.Message.Contains("duplicate") ? "Error: Data already exists." : $"Server error: {ex.Message}";
            lblRegMessage.IsVisible = true;
        }
        finally
        {
            btnSubmitRegister.IsEnabled = true;
        }
    }

    private bool ValidateRegex(string input, string pattern) => Regex.IsMatch(input, pattern);
    private void ShowRegError(TextBlock label, string message) { label.Text = "⚠ " + message; label.IsVisible = true; }
    private void HideRegError(TextBlock label) => label.IsVisible = false;
}