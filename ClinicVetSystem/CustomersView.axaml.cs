using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class CustomersView : UserControl
{
    private List<CustomerRow> _allCustomers = new();
    private readonly ObservableCollection<CustomerRow> _displayedCustomers = new();
    private bool _isEditMode = false;

    public CustomersView()
    {
        InitializeComponent();
        lstCustomers.ItemsSource = _displayedCustomers;
    }

    public async Task LoadDataAsync()
    {
        try {
            lblStatus.Text = "Syncing...";
            lblStatus.Foreground = Avalonia.Media.SolidColorBrush.Parse("#64748b");
            var result = await SupabaseService.Client!.From<Customer>().Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending).Get();
            _allCustomers = result.Models.Select(c => new CustomerRow(c)).ToList();
            RefreshDisplay();
            lblStatus.Text = "Up to date ✓";
            lblStatus.Foreground = Avalonia.Media.SolidColorBrush.Parse("#10b981");
        } catch (Exception) { 
            lblStatus.Text = "⚠ שגיאה בטעינת נתונים (בדוק חיבור רשת)."; 
            lblStatus.Foreground = Avalonia.Media.SolidColorBrush.Parse("#dc2626");
        }
    }

    private void RefreshDisplay()
    {
        var query = txtSearch.Text?.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(query) 
            ? _allCustomers 
            : _allCustomers.Where(c => c.Id.Contains(query) || c.Phone.Contains(query)).ToList();
            
        _displayedCustomers.Clear();
        foreach (var row in filtered) _displayedCustomers.Add(row);
        lblCustomerCount.Text = $"Showing {filtered.Count} clients";
    }

    private void txtSearch_TextChanged(object? sender, TextChangedEventArgs e) => RefreshDisplay();
    
    private void lstCustomers_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = lstCustomers.SelectedItem != null;
        btnEdit.IsEnabled = hasSelection;
        btnDelete.IsEnabled = hasSelection;
        btnViewPets.IsEnabled = hasSelection;
    }

    // ─── Dialog Logic ──────────────────────────────────────────────────

    private void OpenDialog(Customer? c = null)
    {
        _isEditMode = c != null;
        lblDialogTitle.Text = _isEditMode ? "Edit Client" : "Register Client";
        txtDialogId.Text = c?.Id ?? "";
        txtDialogId.IsReadOnly = _isEditMode;
        txtDialogId.Opacity = _isEditMode ? 0.6 : 1.0;
        txtDialogName.Text = c?.FullName ?? "";
        txtDialogPhone.Text = c?.Phone ?? "";
        txtDialogEmail.Text = c?.Email ?? "";
        lblDialogError.IsVisible = false;
        ModalOverlay.IsVisible = true;
    }

    private void btnCancelDialog_Click(object? sender, RoutedEventArgs e) => ModalOverlay.IsVisible = false;

    private async void btnSaveDialog_Click(object? sender, RoutedEventArgs e)
    {
        try {
            var name = txtDialogName.Text?.Trim() ?? "";
            var id = txtDialogId.Text?.Trim() ?? "";
            var phone = txtDialogPhone.Text?.Trim() ?? "";
            var email = txtDialogEmail.Text?.Trim() ?? "";
            
            // ולידציה שם (אותיות ורווחים)
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^[\u0590-\u05FFa-zA-Z\s]+$"))
            {
                ShowDialogError("שם הלקוח חייב להכיל אותיות בלבד.");
                return;
            }
            
            // ולידציה ת"ז (9 ספרות)
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"^\d{9}$"))
            {
                ShowDialogError("תעודת הזהות חייבת להכיל בדיוק 9 ספרות.");
                return;
            }

            // תיקון באג 3: ולידציית טלפון (מתחיל ב-0, ויש בו 9 או 10 ספרות)
            if (string.IsNullOrEmpty(phone) || !Regex.IsMatch(phone, @"^0\d{8,9}$"))
            {
                ShowDialogError("מספר הטלפון לא חוקי (חייב להתחיל ב-0 ולהכיל 9-10 ספרות).");
                return;
            }

            // תיקון באג 4 + הכנה ל-9: ולידציית אימייל מחמירה אם הוקלד משהו
            if (!string.IsNullOrEmpty(email))
            {
                // מאשר רק סיומות ידועות וחוסם דברים כמו .fucku
                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.(com|co\.il|org|net|ac\.il|gov\.il|edu)$", RegexOptions.IgnoreCase))
                {
                    ShowDialogError("כתובת האימייל אינה תקינה או בעלת סיומת לא חוקית.");
                    return;
                }
            }

            var customer = new Customer { 
                Id = id, 
                FullName = name, 
                Phone = phone, 
                Email = string.IsNullOrEmpty(email) ? null : email 
            };

            var client = SupabaseService.Client;
            if (client == null) return;

            if (_isEditMode) {
                await client.From<Customer>().Where(x => x.Id == customer.Id)
                    .Set(x => x.FullName!, customer.FullName)
                    .Set(x => x.Phone!, customer.Phone)
                    .Set(x => x.Email!, customer.Email ?? "")
                    .Update();
            } else {
                await client.From<Customer>().Insert(customer);
            }
            
            ModalOverlay.IsVisible = false;
            await LoadDataAsync();
            
        } catch (Exception ex) { 
            if (ex.Message.Contains("duplicate") || ex.Message.Contains("23505"))
                ShowDialogError("שגיאה: תעודת הזהות או האימייל כבר קיימים במערכת.");
            else
                ShowDialogError("שגיאה: ודא שכל השדות הוזנו כראוי."); 
        }
    }
    
    private void ShowDialogError(string msg)
    {
        lblDialogError.Text = "⚠ " + msg;
        lblDialogError.IsVisible = true;
    }

    private void btnAddCustomer_Click(object? sender, RoutedEventArgs e) => OpenDialog();
    
    private void btnEdit_Click(object? sender, RoutedEventArgs e) 
    {
        if (lstCustomers.SelectedItem is CustomerRow r) OpenDialog(new Customer { Id = r.Id, FullName = r.FullName, Phone = r.Phone, Email = r.Email });
    }

    private async void btnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (lstCustomers.SelectedItem is not CustomerRow r) return;
        try {
            await SupabaseService.Client!.From<Customer>().Where(c => c.Id == r.Id).Delete();
            await LoadDataAsync();
        } catch (Exception) { 
            lblStatus.Text = "⚠ שגיאה: לא ניתן למחוק לקוח שיש לו חיות מחמד במערכת."; 
            lblStatus.Foreground = Avalonia.Media.SolidColorBrush.Parse("#dc2626");
        }
    }

    private void btnViewPets_Click(object? sender, RoutedEventArgs e)
    {
        if (lstCustomers.SelectedItem is not CustomerRow r) return;
        if (this.VisualRoot is MainMenuWindow mainWindow)
        {
            mainWindow.GoToPetsForCustomer(r.Id);
        }
    }
}

public class CustomerRow
{
    public string Id { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string? Email { get; set; }
    public string CreatedAtFormatted { get; set; }
    public CustomerRow(Customer c) {
        Id = c.Id; FullName = c.FullName; Phone = c.Phone; Email = c.Email;
        CreatedAtFormatted = c.CreatedAt?.ToString("MMM dd, yyyy") ?? "—";
    }
}