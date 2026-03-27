using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class CustomerDialog : Window
{
    // אם עורכים לקוח קיים — שמור את ה-id המקורי
    private readonly bool _isEdit;

    // הלקוח שיוחזר לחלון הקורא
    public Customer? ResultCustomer { get; private set; }

    // ── Constructor: הוספה חדשה ─────────────────────────────────────────────
    public CustomerDialog()
    {
        InitializeComponent();
        _isEdit = false;
        lblTitle.Text = "הוספת לקוח חדש";
    }

    // ── Constructor: עריכה ──────────────────────────────────────────────────
    public CustomerDialog(Customer existing)
    {
        InitializeComponent();
        _isEdit = true;
        lblTitle.Text = "עריכת פרטי לקוח";

        // מלא את השדות עם הנתונים הקיימים
        txtId.Text      = existing.Id;
        txtFullName.Text = existing.FullName;
        txtPhone.Text   = existing.Phone;
        txtEmail.Text   = existing.Email ?? "";

        // בעריכה — לא מאפשרים שינוי ת"ז
        txtId.IsReadOnly = true;
        txtId.Opacity    = 0.5;
    }

    // ── ולידציות ────────────────────────────────────────────────────────────

    private (bool valid, string error) ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (false, "ת\"ז הוא שדה חובה");
        if (!Regex.IsMatch(id, @"^\d{9}$"))
            return (false, "ת\"ז חייב להכיל בדיוק 9 ספרות");
        return (true, "");
    }

    private (bool valid, string error) ValidateFullName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "שם מלא הוא שדה חובה");
        if (name.Trim().Length < 2)
            return (false, "שם מלא חייב להכיל לפחות 2 תווים");
        if (name.Trim().Length > 50)
            return (false, "שם מלא לא יכול לעלות על 50 תווים");
        return (true, "");
    }

    private (bool valid, string error) ValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return (false, "טלפון הוא שדה חובה");
        var clean = phone.Replace("-", "").Replace(" ", "");
        if (!Regex.IsMatch(clean, @"^05\d{8}$"))
            return (false, "מספר טלפון לא תקין (דוגמה: 0501234567)");
        return (true, "");
    }

    private (bool valid, string error) ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (true, ""); // אופציונלי
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return (false, "כתובת אימייל לא תקינה");
        return (true, "");
    }

    // ── כפתור שמירה ─────────────────────────────────────────────────────────
    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        bool allValid = true;

        // ולידציה ת"ז
        var idVal = ValidateId(txtId.Text ?? "");
        lblIdError.Text      = idVal.error;
        lblIdError.IsVisible = !idVal.valid;
        if (!idVal.valid) allValid = false;

        // ולידציה שם מלא
        var nameVal = ValidateFullName(txtFullName.Text ?? "");
        lblFullNameError.Text      = nameVal.error;
        lblFullNameError.IsVisible = !nameVal.valid;
        if (!nameVal.valid) allValid = false;

        // ולידציה טלפון
        var phoneVal = ValidatePhone(txtPhone.Text ?? "");
        lblPhoneError.Text      = phoneVal.error;
        lblPhoneError.IsVisible = !phoneVal.valid;
        if (!phoneVal.valid) allValid = false;

        // ולידציה אימייל
        var emailVal = ValidateEmail(txtEmail.Text ?? "");
        lblEmailError.Text      = emailVal.error;
        lblEmailError.IsVisible = !emailVal.valid;
        if (!emailVal.valid) allValid = false;

        if (!allValid) return;

        // כל הולידציות עברו — בנה אובייקט לקוח
        ResultCustomer = new Customer
        {
            Id       = (txtId.Text ?? "").Trim(),
            FullName = (txtFullName.Text ?? "").Trim(),
            Phone    = (txtPhone.Text ?? "").Replace("-", "").Replace(" ", ""),
            Email    = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim()
        };

        Close(ResultCustomer);
    }

    // ── כפתור ביטול ─────────────────────────────────────────────────────────
    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
