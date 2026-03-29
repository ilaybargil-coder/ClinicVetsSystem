using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ClinicVetsSystem.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClinicVetsSystem;

public partial class MainWindow : Window
{
    private bool _isLoginView = true;
    
    // משתני מערכת ה-OTP
    private string _currentOtpCode = "";
    private string _otpTargetEmail = "";
    private string _otpMode = ""; // "Register" או "ResetPassword"
    private Staff? _pendingRegisterStaff = null;
    
    // הכנס כאן את פרטי החשבון שפתחת עבור הפרויקט!
    private readonly string _systemEmail = "clinic.vets.system@gmail.com";
    private readonly string _systemEmailAppPassword = "lrhh susg gnrl buwx"; 

    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e) { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) this.BeginMoveDrag(e); }
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
            OverlayLeftContent.Opacity = 1; OverlayLeftContent.IsHitTestVisible = true;
            OverlayRightContent.Opacity = 0; OverlayRightContent.IsHitTestVisible = false;
        }
        else
        {
            OverlayLeftContent.Opacity = 0; OverlayLeftContent.IsHitTestVisible = false;
            OverlayRightContent.Opacity = 1; OverlayRightContent.IsHitTestVisible = true;
        }
    }

    private async void btnLoginSubmit_Click(object sender, RoutedEventArgs e)
    {
        string username = txtLoginUsername.Text?.Trim() ?? ""; 
        string password = txtLoginPassword.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowLoginError("נא להזין שם משתמש וסיסמה.");
            return;
        }

        btnLoginSubmit.IsEnabled = false;
        lblLoginMessage.IsVisible = false;

        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var result = await client.From<Staff>().Where(s => s.Username == username && s.Password == password).Get();
            var staffMember = result.Models.FirstOrDefault();

            if (staffMember != null)
            {
                new MainMenuWindow(staffMember).Show();
                this.Close();
            }
            else
            {
                ShowLoginError("שם משתמש או סיסמה שגויים.");
            }
        }
        catch (Exception) { ShowLoginError("שגיאת התחברות לשרת הנתונים."); }
        finally { btnLoginSubmit.IsEnabled = true; }
    }

    private void ShowLoginError(string msg)
    {
        lblLoginMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
        lblLoginMessage.Background = SolidColorBrush.Parse("#1Affdad6");
        lblLoginMessage.Text = msg;
        lblLoginMessage.IsVisible = true;
    }

    private async void btnSubmitRegister_Click(object sender, RoutedEventArgs e)
    {
        string username = txtRegUsername.Text?.Trim() ?? "";
        string password = txtRegPassword.Text ?? "";
        string id = txtRegId.Text?.Trim() ?? "";
        string email = txtRegEmail.Text?.Trim() ?? "";
        string role = (cmbRegRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        bool isValid = true;

        if (!ValidateRegex(username, @"^[a-zA-Z0-9]{6,8}$") || username.Count(char.IsDigit) > 2)
        { ShowRegError(lblUsernameError, "שם משתמש: 6-8 תווים, מקס' 2 ספרות."); isValid = false; }
        else HideRegError(lblUsernameError);

        bool passOk = password.Length >= 8 && password.Length <= 10 && password.Any(char.IsLetter) && password.Count(char.IsDigit) == 1 && password.Count(c => "$#!".Contains(c)) == 1;
        if (!passOk)
        { ShowRegError(lblPasswordError, "סיסמה: 8-10 תווים, אות, ספרה אחת, ותו מיוחד ($#!)."); isValid = false; }
        else HideRegError(lblPasswordError);

        // אנחנו ממשיכים לבדוק שהמשתמש מקליד 9 ספרות, גם אם זה לא נשמר ב-DB
        if (!ValidateRegex(id, @"^\d{9}$"))
        { ShowRegError(lblIdError, "ת\"ז: בדיוק 9 ספרות."); isValid = false; }
        else HideRegError(lblIdError);

        if (!ValidateRegex(email, @"^[^@\s]+@[^@\s]+\.(com|co\.il|ac\.il|org|net|gov\.il|edu)$"))
        { ShowRegError(lblEmailError, "כתובת אימייל אינה תקינה או בעלת סיומת לא חוקית."); isValid = false; }
        else HideRegError(lblEmailError);

        if (string.IsNullOrEmpty(role))
        { ShowRegError(lblRoleError, "יש לבחור תפקיד."); isValid = false; }
        else HideRegError(lblRoleError);

        if (!isValid) return;

        btnSubmitRegister.IsEnabled = false;
        lblRegMessage.IsVisible = false;

        try
        {
            var client = SupabaseService.Client;
            
            var checkEmail = await client!.From<Staff>().Where(s => s.Email == email).Get();
            var checkUsername = await client.From<Staff>().Where(s => s.Username == username).Get();

            if (checkEmail.Models.Count > 0 || checkUsername.Models.Count > 0)
            {
                lblRegMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
                lblRegMessage.Background = SolidColorBrush.Parse("#1Affdad6");
                lblRegMessage.Text = "שגיאה: שם המשתמש או האימייל כבר קיימים במערכת.";
                lblRegMessage.IsVisible = true;
                btnSubmitRegister.IsEnabled = true;
                return;
            }

            // הנה התיקון הקריטי! יצירת אובייקט נקי לחלוטין בלי זכר למילה Id
            _pendingRegisterStaff = new Staff 
            { 
                Username = username, 
                Password = password, 
                Email = email, 
                Role = role 
            };
            
            _otpMode = "Register";
            _otpTargetEmail = email;

            await GenerateAndSendOtpAsync(email);
            ShowOtpModal(pnlVerifyOtp);
        }
        catch (Exception ex) 
        { 
            lblRegMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
            lblRegMessage.Background = SolidColorBrush.Parse("#1Affdad6");
            lblRegMessage.Text = "שגיאת מערכת: " + ex.Message;
            lblRegMessage.IsVisible = true;
        }
        finally 
        { 
            btnSubmitRegister.IsEnabled = true; 
        }
    }
    private void ShowOtpModal(Border targetPanel)
    {
        pnlRequestReset.IsVisible = false;
        pnlVerifyOtp.IsVisible = false;
        pnlNewPassword.IsVisible = false;
        targetPanel.IsVisible = true;
        ModalOverlay.IsVisible = true;
        
        txtOtpCode.Text = "";
        lblOtpError.IsVisible = false;
        lblResetError.IsVisible = false;
    }

    private void btnCancelModal_Click(object sender, RoutedEventArgs e) => ModalOverlay.IsVisible = false;

    private async Task GenerateAndSendOtpAsync(string toEmail)
    {
        _currentOtpCode = new Random().Next(100000, 999999).ToString();
        
        try
        {
            var smtp = new SmtpClient { Host = "smtp.gmail.com", Port = 587, EnableSsl = true, DeliveryMethod = SmtpDeliveryMethod.Network, UseDefaultCredentials = false, Credentials = new NetworkCredential(_systemEmail, _systemEmailAppPassword) };
            using var message = new MailMessage(new MailAddress(_systemEmail, "Clinic Vets System"), new MailAddress(toEmail))
            {
                Subject = "ClinicVetsSystem - Your Verification Code",
                Body = $"Hello,\n\nYour one-time verification code is: {_currentOtpCode}\n\nPlease enter this code in the app to continue.\n\nThank you!"
            };
            await smtp.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Fake OTP sent: {_currentOtpCode} (Email setup failed: {ex.Message})");
        }
    }

    private async void btnVerifyOtp_Click(object sender, RoutedEventArgs e)
    {
        if (txtOtpCode.Text != _currentOtpCode)
        {
            lblOtpError.Text = "⚠ Invalid code. Please try again.";
            lblOtpError.IsVisible = true;
            return;
        }

        if (_otpMode == "Register" && _pendingRegisterStaff != null)
        {
            try
            {
                // התיקון: שימוש בשם העמודה כפי שהוא כתוב בדיוק במסד הנתונים (אותיות קטנות וקו תחתון)
                var maxStaff = await SupabaseService.Client!.From<Staff>()
                    .Select("employee_number")
                    .Order("employee_number", Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();
                    
                int nextEmployeeNumber = maxStaff.Models.FirstOrDefault()?.EmployeeNumber + 1 ?? 1000; 
                
                _pendingRegisterStaff.EmployeeNumber = nextEmployeeNumber;
                
                await SupabaseService.Client!.From<Staff>().Insert(_pendingRegisterStaff);
                
                ModalOverlay.IsVisible = false;
                lblRegMessage.Foreground = SolidColorBrush.Parse("#006c49");
                lblRegMessage.Background = SolidColorBrush.Parse("#d1fae5");
                lblRegMessage.Text = $"✓ Employee Registered Successfully! (Employee ID: {nextEmployeeNumber})";
                lblRegMessage.IsVisible = true;
                
                txtRegUsername.Text = txtRegPassword.Text = txtRegId.Text = txtRegEmail.Text = "";
                cmbRegRole.SelectedIndex = -1;
                
                await Task.Delay(3000);
                lblRegMessage.IsVisible = false;
                ToggleSlide_Click(null!, null!);
            }
            catch (Exception ex) 
            { 
                // עכשיו, אם תהיה שגיאה, אנחנו נראה *בדיוק* למה Supabase מסרב לשמור!
                lblOtpError.Text = "⚠ DB Error: " + ex.Message; 
                lblOtpError.IsVisible = true; 
            }
        }
        else if (_otpMode == "ResetPassword")
        {
            ShowOtpModal(pnlNewPassword); 
        }
    }

    private void btnForgotPassword_Click(object sender, RoutedEventArgs e) => ShowOtpModal(pnlRequestReset);

    private async void btnSendResetOtp_Click(object sender, RoutedEventArgs e)
    {
        string email = txtResetEmail.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(email)) { lblResetError.Text = "⚠ Enter an email."; lblResetError.IsVisible = true; return; }

        try
        {
            var result = await SupabaseService.Client!.From<Staff>().Where(s => s.Email == email).Get();
            if (result.Models.Count == 0)
            {
                lblResetError.Text = "⚠ Email not found in the system.";
                lblResetError.IsVisible = true;
                return;
            }

            _otpMode = "ResetPassword";
            _otpTargetEmail = email;
            await GenerateAndSendOtpAsync(email);
            ShowOtpModal(pnlVerifyOtp);
        }
        catch (Exception) { lblResetError.Text = "⚠ Database connection error."; lblResetError.IsVisible = true; }
    }

    private async void btnSaveNewPassword_Click(object sender, RoutedEventArgs e)
    {
        string newPass = txtNewPassword.Text ?? "";
        string confirmPass = txtConfirmNewPassword.Text ?? "";

        bool passOk = newPass.Length >= 8 && newPass.Length <= 10 && newPass.Any(char.IsLetter) && newPass.Count(char.IsDigit) == 1 && newPass.Count(c => "$#!".Contains(c)) == 1;
        
        if (!passOk) { lblNewPasswordError.Text = "⚠ Password does not meet criteria."; lblNewPasswordError.IsVisible = true; return; }
        if (newPass != confirmPass) { lblNewPasswordError.Text = "⚠ Passwords do not match."; lblNewPasswordError.IsVisible = true; return; }

        try
        {
            var client = SupabaseService.Client;
            await client!.From<Staff>().Where(s => s.Email == _otpTargetEmail).Set(s => s.Password!, newPass).Update();
            ModalOverlay.IsVisible = false;
        }
        catch (Exception) { lblNewPasswordError.Text = "⚠ Error updating password."; lblNewPasswordError.IsVisible = true; }
    }

    private bool ValidateRegex(string input, string pattern) => Regex.IsMatch(input, pattern);
    private void ShowRegError(TextBlock label, string message) { label.Text = "⚠ " + message; label.IsVisible = true; }
    private void HideRegError(TextBlock label) => label.IsVisible = false;
    
    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TextBox txt)
        {
            if (txt.PasswordChar == '•')
            {
                txt.PasswordChar = '\0'; // מבטל את הקידוד וחושף את הטקסט
                btn.Foreground = SolidColorBrush.Parse("#10b981"); // צובע את העין בירוק כדי לבלוט
            }
            else
            {
                txt.PasswordChar = '•'; // מסתיר חזרה
                btn.Foreground = SolidColorBrush.Parse("#6c7a71"); // מחזיר לאפור הרגיל
            }
        }
    }
}