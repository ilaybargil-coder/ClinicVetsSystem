using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClinicVetsSystem.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClinicVetsSystem;

public partial class RegisterWindow : Window
{
    public RegisterWindow()
    {
        InitializeComponent();
    }

    // ─── Validation Methods ───────────────────────────────────────────────────

    // שם משתמש: 6-8 תווים, מקסימום 2 ספרות, השאר אותיות אנגלית בלבד
    private (bool valid, string error) ValidateUsername(string username)
    {
        if (username.Length < 6 || username.Length > 8)
            return (false, "שם משתמש חייב להיות בין 6-8 תווים.");

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$"))
            return (false, "שם משתמש יכול להכיל אותיות אנגלית וספרות בלבד.");

        int digitCount = username.Count(char.IsDigit);
        if (digitCount > 2)
            return (false, "שם משתמש יכול להכיל לכל היותר 2 ספרות.");

        return (true, "");
    }

    // סיסמה: 8-10 תווים, לפחות אות אחת, בדיוק ספרה אחת, תו מיוחד אחד ($,#,!,.)
    private (bool valid, string error) ValidatePassword(string password)
    {
        if (password.Length < 8 || password.Length > 10)
            return (false, "סיסמה חייבת להיות בין 8-10 תווים.");

        int letterCount  = password.Count(char.IsLetter);
        int digitCount   = password.Count(char.IsDigit);
        int specialCount = password.Count(c => "$#!.".Contains(c));

        if (letterCount < 1)
            return (false, "סיסמה חייבת להכיל לפחות אות אחת.");

        if (digitCount != 1)
            return (false, "סיסמה חייבת להכיל בדיוק ספרה אחת.");

        if (specialCount != 1)
            return (false, "סיסמה חייבת להכיל בדיוק תו מיוחד אחד ($, #, !, .).");

        int validChars = letterCount + digitCount + specialCount;
        if (validChars != password.Length)
            return (false, "סיסמה מכילה תווים לא מורשים.");

        return (true, "");
    }

    // מספר עובד: בדיוק 4 ספרות
    private (bool valid, string error) ValidateEmployeeNumber(string empNum)
    {
        if (!Regex.IsMatch(empNum, @"^\d{4}$"))
            return (false, "מספר עובד חייב להיות בדיוק 4 ספרות.");

        return (true, "");
    }

    // ת"ז: בדיוק 9 ספרות
    private (bool valid, string error) ValidateId(string id)
    {
        if (!Regex.IsMatch(id, @"^\d{9}$"))
            return (false, "ת\"ז חייבת להכיל בדיוק 9 ספרות.");

        return (true, "");
    }

    // אימייל: פורמט תקני עם @
    private (bool valid, string error) ValidateEmail(string email)
    {
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return (false, "כתובת אימייל אינה תקינה.");

        return (true, "");
    }

    // ─── Show/Hide error helpers ──────────────────────────────────────────────

    private void ShowError(TextBlock label, string message)
    {
        label.Text = "⚠ " + message;
        label.IsVisible = true;
    }

    private void HideError(TextBlock label) => label.IsVisible = false;

    // ─── Register Button Click ────────────────────────────────────────────────

    private async void btnRegister_Click(object sender, RoutedEventArgs e)
    {
        string username     = txtUsername.Text?.Trim() ?? "";
        string password     = txtPassword.Text ?? "";
        string employeeNum  = txtEmployeeNumber.Text?.Trim() ?? "";
        string id           = txtId.Text?.Trim() ?? "";
        string email        = txtEmail.Text?.Trim() ?? "";
        string role         = (cmbRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        // Validate all fields
        bool isValid = true;

        var (usernameOk, usernameErr) = ValidateUsername(username);
        if (!usernameOk) { ShowError(lblUsernameError, usernameErr); isValid = false; }
        else HideError(lblUsernameError);

        var (passwordOk, passwordErr) = ValidatePassword(password);
        if (!passwordOk) { ShowError(lblPasswordError, passwordErr); isValid = false; }
        else HideError(lblPasswordError);

        var (empNumOk, empNumErr) = ValidateEmployeeNumber(employeeNum);
        if (!empNumOk) { ShowError(lblEmployeeNumberError, empNumErr); isValid = false; }
        else HideError(lblEmployeeNumberError);

        var (idOk, idErr) = ValidateId(id);
        if (!idOk) { ShowError(lblIdError, idErr); isValid = false; }
        else HideError(lblIdError);

        var (emailOk, emailErr) = ValidateEmail(email);
        if (!emailOk) { ShowError(lblEmailError, emailErr); isValid = false; }
        else HideError(lblEmailError);

        if (string.IsNullOrEmpty(role))
        {
            ShowError(lblRoleError, "יש לבחור תפקיד.");
            isValid = false;
        }
        else HideError(lblRoleError);

        if (!isValid) return;

        // All validations passed — insert to Supabase
        btnRegister.IsEnabled = false;
        HideError(lblMessage);

        try
        {
            var newStaff = new Staff
            {
                Username       = username,
                Password       = password,
                EmployeeNumber = int.Parse(employeeNum),
                Email          = email,
                Role           = role
            };

            await SupabaseService.Client!
                .From<Staff>()
                .Insert(newStaff);

            // Success!
            lblMessage.Foreground = Brushes.LightGreen;
            lblMessage.Text = "✓ העובד נרשם בהצלחה! ניתן להתחבר עכשיו.";
            lblMessage.IsVisible = true;

            // Clear form
            txtUsername.Text       = "";
            txtPassword.Text       = "";
            txtEmployeeNumber.Text = "";
            txtId.Text             = "";
            txtEmail.Text          = "";
            cmbRole.SelectedItem   = null;
        }
        catch (Exception ex)
        {
            lblMessage.Foreground = Brushes.Red;
            lblMessage.Text = ex.Message.Contains("duplicate")
                ? "שגיאה: שם משתמש, מספר עובד או אימייל כבר קיימים במערכת."
                : $"שגיאת שרת: {ex.Message}";
            lblMessage.IsVisible = true;
        }
        finally
        {
            btnRegister.IsEnabled = true;
        }
    }

    private void btnBack_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
