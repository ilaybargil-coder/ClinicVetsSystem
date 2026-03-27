using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class CustomersWindow : Window
{
    private List<CustomerRow> _allCustomers = new();
    private ObservableCollection<CustomerRow> _displayedCustomers = new();

    public CustomersWindow()
    {
        InitializeComponent();
        lstCustomers.ItemsSource = _displayedCustomers;
        _ = LoadCustomersAsync();
    }

    // ─── טעינת נתונים מ-Supabase ────────────────────────────────────────────
    private async Task LoadCustomersAsync()
    {
        try
        {
            lblStatus.Text = "טוען נתונים...";

            var result = await SupabaseService.Client!
                .From<Customer>()
                .Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending)
                .Get();

            _allCustomers = result.Models
                .Select(c => new CustomerRow(c))
                .ToList();

            RefreshDisplay();
            lblStatus.Text = $"נטענו {_allCustomers.Count} לקוחות בהצלחה";
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"שגיאה בטעינת נתונים: {ex.Message}";
        }
    }

    // ─── רענון תצוגה לפי חיפוש ──────────────────────────────────────────────
    private void RefreshDisplay()
    {
        var query = txtSearch.Text?.Trim().ToLower() ?? "";

        var filtered = string.IsNullOrEmpty(query)
            ? _allCustomers
            : _allCustomers.Where(c =>
                c.FullName.ToLower().Contains(query) ||
                c.Id.Contains(query)
            ).ToList();

        _displayedCustomers.Clear();
        foreach (var row in filtered)
            _displayedCustomers.Add(row);

        lblCount.Text = _allCustomers.Count == filtered.Count
            ? $"{_allCustomers.Count} לקוחות"
            : $"מוצגים {filtered.Count} מתוך {_allCustomers.Count}";
    }

    // ─── חיפוש ──────────────────────────────────────────────────────────────
    private void txtSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        btnClearSearch.IsVisible = !string.IsNullOrEmpty(txtSearch.Text);
        RefreshDisplay();
    }

    private void btnClearSearch_Click(object? sender, RoutedEventArgs e)
    {
        txtSearch.Text = "";
    }

    // ─── בחירה ברשימה ───────────────────────────────────────────────────────
    private void lstCustomers_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = lstCustomers.SelectedItem as CustomerRow;
        var hasSelection = selected != null;

        btnEdit.IsEnabled   = hasSelection;
        btnDelete.IsEnabled = hasSelection;

        lblSelected.Text = hasSelection ? $"נבחר: {selected!.FullName}" : "";
    }

    // ─── הוספת לקוח ─────────────────────────────────────────────────────────
    private async void btnAdd_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new CustomerDialog();
        var result = await dialog.ShowDialog<Customer?>(this);
        if (result == null) return;

        try
        {
            lblStatus.Text = "בודק ת\"ז...";

            var existing = await SupabaseService.Client!
                .From<Customer>()
                .Where(c => c.Id == result.Id)
                .Get();

            if (existing.Models.Count > 0)
            {
                await ShowMessage("שגיאה", $"לקוח עם ת\"ז {result.Id} כבר קיים במערכת.");
                lblStatus.Text = "הוספה נכשלה — ת\"ז כבר קיים";
                return;
            }

            await SupabaseService.Client!.From<Customer>().Insert(result);
            lblStatus.Text = $"✓ לקוח {result.FullName} נוסף בהצלחה";
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"שגיאה: {ex.Message}";
        }
    }

    // ─── עריכת לקוח ─────────────────────────────────────────────────────────
    private async void btnEdit_Click(object? sender, RoutedEventArgs e)
    {
        var selectedRow = lstCustomers.SelectedItem as CustomerRow;
        if (selectedRow == null) return;

        var customer = new Customer
        {
            Id       = selectedRow.Id,
            FullName = selectedRow.FullName,
            Phone    = selectedRow.Phone,
            Email    = selectedRow.Email
        };

        var dialog = new CustomerDialog(customer);
        var result = await dialog.ShowDialog<Customer?>(this);
        if (result == null) return;

        try
        {
            lblStatus.Text = "מעדכן...";

            await SupabaseService.Client!
                .From<Customer>()
                .Where(c => c.Id == result.Id)
                .Set(c => c.FullName!, result.FullName)
                .Set(c => c.Phone!, result.Phone)
                .Set(c => c.Email!, result.Email ?? "")
                .Update();

            lblStatus.Text = $"✓ פרטי {result.FullName} עודכנו בהצלחה";
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"שגיאה: {ex.Message}";
        }
    }

    // ─── מחיקת לקוח ─────────────────────────────────────────────────────────
    private async void btnDelete_Click(object? sender, RoutedEventArgs e)
    {
        var selectedRow = lstCustomers.SelectedItem as CustomerRow;
        if (selectedRow == null) return;

        var confirmed = await ShowConfirm(
            "אישור מחיקה",
            $"האם למחוק את הלקוח {selectedRow.FullName}?\nפעולה זו אינה הפיכה.");
        if (!confirmed) return;

        try
        {
            lblStatus.Text = "מוחק...";

            await SupabaseService.Client!
                .From<Customer>()
                .Where(c => c.Id == selectedRow.Id)
                .Delete();

            lblStatus.Text = $"✓ לקוח {selectedRow.FullName} נמחק";
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"שגיאה: {ex.Message}";
        }
    }

    // ─── חזרה ───────────────────────────────────────────────────────────────
    private void btnBack_Click(object? sender, RoutedEventArgs e) => Close();

    // ─── Helper: הודעה ──────────────────────────────────────────────────────
    private async Task ShowMessage(string title, string message)
    {
        var btn = new Button
        {
            Content = "סגור", Width = 100, Height = 38,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6366F1")),
            Foreground = Avalonia.Media.Brushes.White
        };

        var dlg = new Window
        {
            Title = title, Width = 360, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1F2937")),
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24), Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message, Foreground = Avalonia.Media.Brushes.White,
                        FontSize = 14, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    },
                    btn
                }
            }
        };

        btn.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(this);
    }

    // ─── Helper: אישור ──────────────────────────────────────────────────────
    private async Task<bool> ShowConfirm(string title, string message)
    {
        bool confirmed = false;

        var btnYes = new Button
        {
            Content = "כן, מחק", Width = 110, Height = 38,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#7F1D1D")),
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FCA5A5")),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };
        var btnNo = new Button
        {
            Content = "ביטול", Width = 90, Height = 38,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#374151")),
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#9CA3AF")),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };

        var dlg = new Window
        {
            Title = title, Width = 380, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1F2937")),
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24), Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = message, Foreground = Avalonia.Media.Brushes.White,
                        FontSize = 14, TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 12,
                        Children = { btnNo, btnYes }
                    }
                }
            }
        };

        btnYes.Click += (_, _) => { confirmed = true; dlg.Close(); };
        btnNo.Click  += (_, _) => dlg.Close();

        await dlg.ShowDialog(this);
        return confirmed;
    }
}

// ─── Row ViewModel ────────────────────────────────────────────────────────────
public class CustomerRow
{
    public string  Id                 { get; set; }
    public string  FullName           { get; set; }
    public string  Phone              { get; set; }
    public string? Email              { get; set; }
    public string  CreatedAtFormatted { get; set; }

    public CustomerRow(Customer c)
    {
        Id       = c.Id;
        FullName = c.FullName;
        Phone    = c.Phone;
        Email    = c.Email;
        CreatedAtFormatted = c.CreatedAt.HasValue
            ? c.CreatedAt.Value.ToString("dd/MM/yyyy")
            : "—";
    }
}
