using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            var result = await SupabaseService.Client!.From<Customer>().Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending).Get();
            _allCustomers = result.Models.Select(c => new CustomerRow(c)).ToList();
            RefreshDisplay();
            lblStatus.Text = "Up to date ✓";
        } catch (Exception ex) { lblStatus.Text = $"Error: {ex.Message}"; }
    }

    private void RefreshDisplay()
    {
        var query = txtSearch.Text?.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(query) ? _allCustomers : _allCustomers.Where(c => c.FullName.ToLower().Contains(query) || c.Id.Contains(query)).ToList();
        _displayedCustomers.Clear();
        foreach (var row in filtered) _displayedCustomers.Add(row);
        lblCustomerCount.Text = $"Showing {filtered.Count} clients";
    }

    private void txtSearch_TextChanged(object? sender, TextChangedEventArgs e) => RefreshDisplay();
    private void btnClearSearch_Click(object? sender, RoutedEventArgs e) => txtSearch.Text = "";
    private void lstCustomers_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        btnEdit.IsEnabled = btnDelete.IsEnabled = lstCustomers.SelectedItem != null;
    }

    // --- Modal Logic ---
    private void OpenDialog(Customer? c = null)
    {
        _isEditMode = c != null;
        lblDialogTitle.Text = _isEditMode ? "Edit Client" : "Register Client";
        txtDialogId.Text = c?.Id ?? "";
        txtDialogId.IsReadOnly = _isEditMode;
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
            var customer = new Customer { Id = txtDialogId.Text!, FullName = txtDialogName.Text!, Phone = txtDialogPhone.Text!, Email = txtDialogEmail.Text };
            if (_isEditMode) await SupabaseService.Client!.From<Customer>().Where(x => x.Id == customer.Id).Set(x => x.FullName!, customer.FullName).Update();
            else await SupabaseService.Client!.From<Customer>().Insert(customer);
            ModalOverlay.IsVisible = false;
            await LoadDataAsync();
        } catch (Exception ex) { lblDialogError.Text = ex.Message; lblDialogError.IsVisible = true; }
    }

    private void btnAddCustomer_Click(object? sender, RoutedEventArgs e) => OpenDialog();
    private void btnEdit_Click(object? sender, RoutedEventArgs e) 
    {
        if (lstCustomers.SelectedItem is CustomerRow r) OpenDialog(new Customer { Id = r.Id, FullName = r.FullName, Phone = r.Phone, Email = r.Email });
    }

    private async void btnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (lstCustomers.SelectedItem is CustomerRow r) {
            await SupabaseService.Client!.From<Customer>().Where(x => x.Id == r.Id).Delete();
            await LoadDataAsync();
        }
    }
}

// המחלקה שחסרה לך - חייבת להיות בתוך הקובץ או ב-Namespace זמין
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