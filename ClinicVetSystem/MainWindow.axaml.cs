using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ClinicVetsSystem.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

    private void ToggleSlide_Click(object sender, RoutedEventArgs e)
    {
        _isLoginView = !_isLoginView;
        UpdateSlidePosition();
    }

    private void ContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSlidePosition();

    private void UpdateSlidePosition()
    {
        double targetX = _isLoginView ? 0 : LeftPanel.Bounds.Width;
        OverlayPanel.RenderTransform = TransformOperations.Parse($"translateX({targetX}px)");

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


    // ─── Login Logic (Fix Bug 7 & 8) ─────────────────────────────────────────

    private async void btnLoginSubmit_Click(object sender, RoutedEventArgs e)
    {
        string loginIdentifier = txtLoginEmail.Text?.Trim() ?? ""; // יכול להיות אימייל או שם משתמש
        string password = txtLoginPassword.Text ?? "";

        if (string.IsNullOrEmpty(loginIdentifier) || string.IsNullOrEmpty(password))
        {
            ShowLoginError("נא למלא שם משתמש/אימייל וסיסמה.");
            return;
        }

        btnLoginSubmit.IsEnabled = false;
        lblLoginMessage.IsVisible = false;

        try
        {
            var client = SupabaseService.Client;
            if (client == null)
            {
                ShowLoginError("שגיאה: אין חיבור לשרת נתונים.");
                return;
            }

            // משיכת כל מי שהסיסמה שלו תואמת (כדי לאמת צד לקוח על שם משתמש/אימייל במקביל)
            var result = await client
                .From<Staff>()
                .Where(s => s.Password == password)
                .Get();

            // תיקון באג 7: בודק האם מה שהוקלד תואם לשם המשתמש או לאימייל במערכת
            var staffMember = result.Models.FirstOrDefault(s => s.Username == loginIdentifier || s.Email == loginIdentifier);

            if (staffMember != null)
            {
                var mainMenu = new MainMenuWindow(staffMember);
                mainMenu.Show();
                this.Close();
            }
            else
            {
                ShowLoginError("שם משתמש, אימייל או סיסמה שגויים.");
            }
        }
        catch (Exception)
        {
            // תיקון באג 8: טיפול שגיאות ידידותי למשתמש
            ShowLoginError("שגיאת התחברות: אנא בדוק את החיבור לרשת ונסה שוב.");
        }
        finally
        {
            btnLoginSubmit.IsEnabled = true;
        }
    }

    private void ShowLoginError(string msg)
    {
        lblLoginMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
        lblLoginMessage.Background = SolidColorBrush.Parse("#1Affdad6");
        lblLoginMessage.Text = msg;
        lblLoginMessage.IsVisible = true;
    }


    // ─── Registration Logic (Fix Bug 5, 6 & 8) ───────────────────────────────

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

        // תיקון באג 5: הסרת הנקודה מתוך התווים המיוחדים
        bool passOk = password.Length >= 8 && password.Length <= 10 && password.Any(char.IsLetter) && password.Count(char.IsDigit) == 1 && password.Count(c => "$#!".Contains(c)) == 1;
        if (!passOk)
        { ShowRegError(lblPasswordError, "סיסמה: 8-10 תווים, אות, ספרה אחת, ותו מיוחד ($#!)."); isValid = false; }
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
                Id = id, // חשוב להכניס את הת"ז ל-DB
                Username = username,
                Password = password,
                EmployeeNumber = int.Parse(employeeNum),
                Email = email,
                Role = role
            };

            await SupabaseService.Client!.From<Staff>().Insert(newStaff);

            // הצלחה
            lblRegMessage.Foreground = SolidColorBrush.Parse("#006c49");
            lblRegMessage.Background = SolidColorBrush.Parse("#d1fae5");
            lblRegMessage.Text = "✓ נרשמת בהצלחה! מעביר למסך התחברות...";
            lblRegMessage.IsVisible = true;

            // תיקון באג 6: מעבר אוטומטי למסך הלוגין לאחר 2 שניות
            await Task.Delay(2000);
            
            // ניקוי השדות כדי למנוע שמירת נתונים באוויר
            txtRegUsername.Text = txtRegPassword.Text = txtRegEmpNum.Text = txtRegId.Text = txtRegEmail.Text = "";
            cmbRegRole.SelectedIndex = -1;
            lblRegMessage.IsVisible = false;

            ToggleSlide_Click(null!, null!);
        }
        catch (Exception ex)
        {
            // תיקון באג 8: הודעות שגיאה בעברית ולא SQL
            lblRegMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
            lblRegMessage.Background = SolidColorBrush.Parse("#1Affdad6");
            
            if (ex.Message.Contains("duplicate") || ex.Message.Contains("23505"))
                lblRegMessage.Text = "שגיאה: שם המשתמש, תעודת הזהות או האימייל כבר רשומים במערכת.";
            else
                lblRegMessage.Text = "שגיאת מערכת: לא ניתן להשלים את ההרשמה כעת.";
                
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