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

    // Stores validated registration data while waiting for OTP confirmation
    private Staff? _pendingStaff;

    // Stores email for the forgot-password flow
    private string _forgotEmail = "";

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
        // Reset both panels to their default state when sliding
        ShowRegPanel("form");
        ShowLoginPanel("login");
    }

    private void ContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSlidePosition();
    }

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

    // ─── Panel Navigation Helpers ────────────────────────────────────────────

    private void ShowRegPanel(string panel)
    {
        RegFormScroll.IsVisible  = panel == "form";
        RegOtpPanel.IsVisible    = panel == "otp";
    }

    private void ShowLoginPanel(string panel)
    {
        LoginFormPanel.IsVisible    = panel == "login";
        ForgotEmailPanel.IsVisible  = panel == "forgot-email";
        ForgotOtpPanel.IsVisible    = panel == "forgot-otp";
        ForgotNewPassPanel.IsVisible = panel == "forgot-newpass";
    }

    // ─── Login Logic ─────────────────────────────────────────────────────────

    private async void btnLoginSubmit_Click(object sender, RoutedEventArgs e)
    {
        string email    = txtLoginEmail.Text?.Trim() ?? "";
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
                .Where(s => s.Username == email && s.Password == password)
                .Get();

            var staffMember = result.Models.FirstOrDefault();

            if (staffMember != null)
            {
                var mainMenu = new ClinicVetSystem.MainMenuWindow(staffMember);
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
        lblLoginMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
        lblLoginMessage.Text = msg;
        lblLoginMessage.IsVisible = true;
    }

    private void ShowLoginSuccess(string msg)
    {
        lblLoginMessage.Foreground = SolidColorBrush.Parse("#006c49");
        lblLoginMessage.Text = msg;
        lblLoginMessage.IsVisible = true;
    }

    // ─── Registration Logic ──────────────────────────────────────────────────

    private async void btnSubmitRegister_Click(object sender, RoutedEventArgs e)
    {
        string username    = txtRegUsername.Text?.Trim() ?? "";
        string password    = txtRegPassword.Text ?? "";
        string employeeNum = txtRegEmpNum.Text?.Trim() ?? "";
        string id          = txtRegId.Text?.Trim() ?? "";
        string email       = txtRegEmail.Text?.Trim() ?? "";
        string role        = (cmbRegRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        bool isValid = true;

        if (!ValidateRegex(username, @"^[a-zA-Z0-9]{6,8}$") || username.Count(char.IsDigit) > 2)
        { ShowRegError(lblUsernameError, "שם משתמש: 6-8 תווים, מקס' 2 ספרות."); isValid = false; }
        else HideRegError(lblUsernameError);

        bool passOk = password.Length >= 8 && password.Length <= 10 && password.Any(char.IsLetter) && password.Count(char.IsDigit) == 1 && password.Count(c => "$#!.".Contains(c)) == 1;
        if (!passOk)
        { ShowRegError(lblPasswordError, "סיסמה: 8-10 תווים, אות, ספרה אחת, תו מיוחד ($#!.)."); isValid = false; }
        else HideRegError(lblPasswordError);

        if (!ValidateRegex(employeeNum, @"^[1-9]\d{3}$"))
        { ShowRegError(lblEmpNumError, "מספר עובד: בדיוק 4 ספרות (1000–9999)."); isValid = false; }
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
            // Store pending staff data
            _pendingStaff = new Staff
            {
                Username       = username,
                Password       = password,
                EmployeeNumber = int.Parse(employeeNum),
                Email          = email,
                Role           = role
            };

            // Send OTP to the provided email
            await OtpService.SendOtpAsync(email, "register");

            // Switch to OTP panel
            lblRegOtpSentTo.Text = $"A 6-digit verification code was sent to:\n{email}";
            txtRegOtp.Text = "";
            lblRegOtpError.IsVisible = false;
            ShowRegPanel("otp");
        }
        catch (Exception ex)
        {
            lblRegMessage.Foreground = SolidColorBrush.Parse("#ba1a1a");
            lblRegMessage.Background = SolidColorBrush.Parse("#1Affdad6");
            lblRegMessage.Text = $"Failed to send verification email: {ex.Message}";
            lblRegMessage.IsVisible = true;
        }
        finally
        {
            btnSubmitRegister.IsEnabled = true;
        }
    }

    private async void btnVerifyRegOtp_Click(object sender, RoutedEventArgs e)
    {
        string code = txtRegOtp.Text?.Trim() ?? "";

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            lblRegOtpError.Text = "⚠ Please enter the 6-digit code.";
            lblRegOtpError.IsVisible = true;
            return;
        }

        if (!OtpService.Verify(_pendingStaff!.Email, code))
        {
            lblRegOtpError.Text = "⚠ Incorrect code or code has expired.";
            lblRegOtpError.IsVisible = true;
            return;
        }

        btnVerifyRegOtp.IsEnabled = false;
        lblRegOtpError.IsVisible = false;

        try
        {
            await SupabaseService.Client!.From<Staff>().Insert(_pendingStaff!);

            // Switch to login and show success message
            ShowRegPanel("form");
            _isLoginView = false;
            ToggleSlide_Click(null!, null!);

            ShowLoginSuccess("✓ Registration successful! Please sign in.");
        }
        catch (Exception ex)
        {
            lblRegOtpError.Text = ex.Message.Contains("duplicate")
                ? "⚠ Error: Data already exists."
                : $"⚠ Server error: {ex.Message}";
            lblRegOtpError.IsVisible = true;
        }
        finally
        {
            btnVerifyRegOtp.IsEnabled = true;
        }
    }

    private async void btnResendRegOtp_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingStaff == null) return;
        try
        {
            await OtpService.SendOtpAsync(_pendingStaff.Email, "register");
            lblRegOtpError.Text = "✓ A new code was sent.";
            lblRegOtpError.Foreground = SolidColorBrush.Parse("#006c49");
            lblRegOtpError.IsVisible = true;
        }
        catch
        {
            lblRegOtpError.Foreground = SolidColorBrush.Parse("#ba1a1a");
            lblRegOtpError.Text = "⚠ Failed to resend. Please try again.";
            lblRegOtpError.IsVisible = true;
        }
    }

    private void btnBackToRegForm_Click(object sender, RoutedEventArgs e)
    {
        _pendingStaff = null;
        ShowRegPanel("form");
    }

    // ─── Forgot Password Flow ────────────────────────────────────────────────

    private void ForgotPassword_Click(object sender, RoutedEventArgs e)
    {
        txtForgotEmail.Text = "";
        lblForgotEmailError.IsVisible = false;
        ShowLoginPanel("forgot-email");
    }

    private async void btnSendForgotOtp_Click(object sender, RoutedEventArgs e)
    {
        string email = txtForgotEmail.Text?.Trim() ?? "";

        if (!ValidateRegex(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            lblForgotEmailError.Text = "⚠ Invalid email address.";
            lblForgotEmailError.IsVisible = true;
            return;
        }

        btnSendForgotOtp.IsEnabled = false;
        lblForgotEmailError.IsVisible = false;

        try
        {
            // Verify email exists in the system
            var result = await SupabaseService.Client!
                .From<Staff>()
                .Where(s => s.Email == email)
                .Get();

            if (!result.Models.Any())
            {
                lblForgotEmailError.Text = "⚠ No account found with this email.";
                lblForgotEmailError.IsVisible = true;
                return;
            }

            _forgotEmail = email;
            await OtpService.SendOtpAsync(email, "reset");

            lblForgotOtpSentTo.Text = $"A 6-digit reset code was sent to:\n{email}";
            txtForgotOtp.Text = "";
            lblForgotOtpError.IsVisible = false;
            ShowLoginPanel("forgot-otp");
        }
        catch (Exception ex)
        {
            lblForgotEmailError.Text = $"⚠ Error: {ex.Message}";
            lblForgotEmailError.IsVisible = true;
        }
        finally
        {
            btnSendForgotOtp.IsEnabled = true;
        }
    }

    private void btnVerifyForgotOtp_Click(object sender, RoutedEventArgs e)
    {
        string code = txtForgotOtp.Text?.Trim() ?? "";

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            lblForgotOtpError.Text = "⚠ Please enter the 6-digit code.";
            lblForgotOtpError.IsVisible = true;
            return;
        }

        if (!OtpService.Verify(_forgotEmail, code))
        {
            lblForgotOtpError.Text = "⚠ Incorrect code or code has expired.";
            lblForgotOtpError.IsVisible = true;
            return;
        }

        txtForgotNewPass.Text = "";
        lblForgotNewPassError.IsVisible = false;
        ShowLoginPanel("forgot-newpass");
    }

    private async void btnResendForgotOtp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await OtpService.SendOtpAsync(_forgotEmail, "reset");
            lblForgotOtpError.Foreground = SolidColorBrush.Parse("#006c49");
            lblForgotOtpError.Text = "✓ A new code was sent.";
            lblForgotOtpError.IsVisible = true;
        }
        catch
        {
            lblForgotOtpError.Foreground = SolidColorBrush.Parse("#ba1a1a");
            lblForgotOtpError.Text = "⚠ Failed to resend. Please try again.";
            lblForgotOtpError.IsVisible = true;
        }
    }

    private void btnBackToLogin_Click(object sender, RoutedEventArgs e)
    {
        ShowLoginPanel("login");
    }

    private void btnBackToForgotEmail_Click(object sender, RoutedEventArgs e)
    {
        ShowLoginPanel("forgot-email");
    }

    private async void btnUpdatePassword_Click(object sender, RoutedEventArgs e)
    {
        string password = txtForgotNewPass.Text ?? "";

        bool passOk = password.Length >= 8 && password.Length <= 10
                      && password.Any(char.IsLetter)
                      && password.Count(char.IsDigit) == 1
                      && password.Count(c => "$#!.".Contains(c)) == 1;

        if (!passOk)
        {
            lblForgotNewPassError.Text = "⚠ 8-10 chars, at least one letter, one digit, one special char ($#!.).";
            lblForgotNewPassError.IsVisible = true;
            return;
        }

        btnUpdatePassword.IsEnabled = false;
        lblForgotNewPassError.IsVisible = false;

        try
        {
            await SupabaseService.Client!
                .From<Staff>()
                .Where(s => s.Email == _forgotEmail)
                .Set(s => s.Password, password)
                .Update();

            // Back to login with success message
            ShowLoginPanel("login");
            ShowLoginSuccess("✓ Password updated! Please sign in with your new password.");
        }
        catch (Exception ex)
        {
            lblForgotNewPassError.Text = $"⚠ Failed to update password: {ex.Message}";
            lblForgotNewPassError.IsVisible = true;
        }
        finally
        {
            btnUpdatePassword.IsEnabled = true;
        }
    }

    // ─── Shared Helpers ──────────────────────────────────────────────────────

    private bool ValidateRegex(string input, string pattern) => Regex.IsMatch(input, pattern);
    private void ShowRegError(TextBlock label, string message) { label.Text = "⚠ " + message; label.IsVisible = true; }
    private void HideRegError(TextBlock label) => label.IsVisible = false;
}
