using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class InventoryView : UserControl
{
    // ── State ──────────────────────────────────────────────────────────────
    private List<MedRow> _allMeds      = new();
    private List<MedRow> _displayedMeds = new();
    private int?         _editingMedId;   // null = add mode

    // ── Constructor ────────────────────────────────────────────────────────
    public InventoryView()
    {
        InitializeComponent();
        _ = LoadDataAsync();
    }

    // ── DB Load ────────────────────────────────────────────────────────────
    private async Task LoadDataAsync()
    {
        try
        {
            lblSubtitle.Text = "טוען נתונים...";

            var medsTask  = SupabaseService.Client!.From<Medication>()
                                .Order(m => m.Name, Postgrest.Constants.Ordering.Ascending)
                                .Get();
            var usageTask = SupabaseService.Client!.From<VisitMedication>().Get();

            await Task.WhenAll(medsTask, usageTask);

            var meds     = medsTask.Result.Models;
            var usageAll = usageTask.Result.Models;

            // Count how many times each medication was prescribed
            var usageMap = usageAll
                .GroupBy(vm => vm.MedicationId)
                .ToDictionary(g => g.Key, g => g.Count());

            _allMeds = meds.Select(m => new MedRow(m, usageMap.GetValueOrDefault(m.Id, 0))).ToList();

            UpdateStats();
            ApplyFilter();
        }
        catch (Exception)
        {
            lblSubtitle.Text = "⚠ שגיאה בטעינת נתונים";
        }
    }

    // ── Stats ──────────────────────────────────────────────────────────────
    private void UpdateStats()
    {
        int total  = _allMeds.Count;
        int low    = _allMeds.Count(m => m.Stock is > 0 and <= 5);
        int outOf  = _allMeds.Count(m => m.Stock == 0);
        decimal val = _allMeds
            .Where(m => m.Stock.HasValue)
            .Sum(m => m.Price * m.Stock!.Value);

        lblStatTotal.Text = total.ToString();
        lblStatLow.Text   = low.ToString();
        lblStatOut.Text   = outOf.ToString();
        lblStatValue.Text = $"₪{val:N0}";
        lblSubtitle.Text  = $"{total} תרופות במלאי";
    }

    // ── Filter ─────────────────────────────────────────────────────────────
    private void TxtSearch_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = txtSearch.Text?.Trim().ToLower() ?? "";
        _displayedMeds = string.IsNullOrEmpty(q)
            ? _allMeds
            : _allMeds.Where(m => m.Name.ToLower().Contains(q)).ToList();

        RenderMedList();
    }

    // ── Render list ────────────────────────────────────────────────────────
    private void RenderMedList()
    {
        MedList.Children.Clear();

        if (_displayedMeds.Count == 0)
        {
            MedList.Children.Add(new Border
            {
                Padding = new Thickness(0, 48),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "💊", FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock
                        {
                            Text = "אין תרופות במלאי",
                            FontSize = 15, FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            });
            return;
        }

        foreach (var med in _displayedMeds)
        {
            MedList.Children.Add(BuildMedRow(med));
            MedList.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.Parse("#f1f5f9")),
                Margin = new Thickness(20, 0)
            });
        }
    }

    // ── Medication row ─────────────────────────────────────────────────────
    private Border BuildMedRow(MedRow med)
    {
        // ── Stock badge ──
        var (stockBg, stockFg, stockIcon, stockText) = med.StockStatus;
        var stockBadge = new Border
        {
            Background   = new SolidColorBrush(Color.Parse(stockBg)),
            CornerRadius = new CornerRadius(12),
            Padding      = new Thickness(12, 5)
        };
        stockBadge.Child = new TextBlock
        {
            Text = $"{stockIcon} {stockText}",
            FontSize = 12, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse(stockFg))
        };

        // ── Usage badge ──
        var usageBadge = new Border
        {
            Background   = new SolidColorBrush(Color.Parse("#f1f5f9")),
            CornerRadius = new CornerRadius(10),
            Padding      = new Thickness(10, 4)
        };
        usageBadge.Child = new TextBlock
        {
            Text = $"נרשמה {med.UsageCount} פעמים",
            FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#64748b"))
        };

        // ── Left: icon + details ──
        var iconBorder = new Border
        {
            Width = 48, Height = 48,
            CornerRadius = new CornerRadius(14),
            Background   = new SolidColorBrush(Color.Parse(stockBg)),
            Child = new TextBlock
            {
                Text = "💊", FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            }
        };

        var nameLine = new TextBlock
        {
            Text = med.Name, FontSize = 15, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1e293b"))
        };
        var subLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        subLine.Children.Add(new TextBlock
        {
            Text = $"₪{med.Price} ליחידה",
            FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#64748b"))
        });
        subLine.Children.Add(usageBadge);

        var detailsPanel = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        detailsPanel.Children.Add(nameLine);
        detailsPanel.Children.Add(subLine);

        // ── Stock adjust: [−] [N] [+] ──
        var stockDisplay = new TextBlock
        {
            Text = med.Stock.HasValue ? med.Stock.Value.ToString() : "—",
            FontSize = 16, FontWeight = FontWeight.Black,
            Foreground = new SolidColorBrush(Color.Parse(stockFg)),
            Width = 32,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var btnMinus = new Button
        {
            Content = "−", Classes = { "iconBtn" },
            IsEnabled = med.Stock is > 0
        };
        var btnPlus = new Button { Content = "+", Classes = { "iconBtn" } };

        var medId = med.Id;
        btnMinus.Click += async (_, _) => await AdjustStock(medId, -1);
        btnPlus.Click  += async (_, _) => await AdjustStock(medId, +1);

        var adjustPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        adjustPanel.Children.Add(btnMinus);
        adjustPanel.Children.Add(stockDisplay);
        adjustPanel.Children.Add(btnPlus);

        // ── Action buttons ──
        var btnEdit = new Button
        {
            Content = "✏", Background = new SolidColorBrush(Color.Parse("#eff6ff")),
            Foreground = new SolidColorBrush(Color.Parse("#3b82f6")),
            CornerRadius = new CornerRadius(10), Width = 34, Height = 34,
            Padding = new Thickness(0),
            FontSize = 14,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment   = VerticalAlignment.Center
        };
        var btnDelete = new Button
        {
            Content = "🗑", Background = new SolidColorBrush(Color.Parse("#fef2f2")),
            Foreground = new SolidColorBrush(Color.Parse("#dc2626")),
            CornerRadius = new CornerRadius(10), Width = 34, Height = 34,
            Padding = new Thickness(0),
            FontSize = 14,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment   = VerticalAlignment.Center
        };

        btnEdit.Click   += (_, _) => OpenEditDialog(med);
        btnDelete.Click += async (_, _) => await DeleteMed(med.Id);

        var actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        actionPanel.Children.Add(btnEdit);
        actionPanel.Children.Add(btnDelete);

        // ── Assemble row ──
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto, Auto"),
            ColumnSpacing = 16
        };
        Grid.SetColumn(iconBorder,   0);
        Grid.SetColumn(detailsPanel, 1);
        Grid.SetColumn(stockBadge,   2);
        Grid.SetColumn(adjustPanel,  3);
        Grid.SetColumn(actionPanel,  4);
        row.Children.Add(iconBorder);
        row.Children.Add(detailsPanel);
        row.Children.Add(stockBadge);
        row.Children.Add(adjustPanel);
        row.Children.Add(actionPanel);

        return new Border
        {
            Padding = new Thickness(20, 16),
            Child   = row
        };
    }

    // ── Stock adjustment ───────────────────────────────────────────────────
    private async Task AdjustStock(int medId, int delta)
    {
        var med = _allMeds.FirstOrDefault(m => m.Id == medId);
        if (med == null) return;

        int current  = med.Stock ?? 0;
        int newStock = Math.Max(0, current + delta);

        try
        {
            await SupabaseService.Client!.From<Medication>()
                .Where(m => m.Id == medId)
                .Set(m => m.Stock, (int?)newStock)
                .Update();

            await LoadDataAsync();
        }
        catch (Exception) { /* ignore transient errors */ }
    }

    // ── Delete medication ──────────────────────────────────────────────────
    private async Task DeleteMed(int medId)
    {
        try
        {
            await SupabaseService.Client!.From<Medication>()
                .Where(m => m.Id == medId)
                .Delete();
            await LoadDataAsync();
        }
        catch (Exception)
        {
            lblSubtitle.Text = "⚠ לא ניתן למחוק תרופה שנרשמה בביקורים";
        }
    }

    // ── Add / Edit dialog ──────────────────────────────────────────────────
    private void BtnAddMed_Click(object? sender, RoutedEventArgs e)
        => OpenAddDialog();

    private void OpenAddDialog()
    {
        _editingMedId          = null;
        lblMedDialogTitle.Text = "תרופה חדשה";
        lblSaveMedBtn.Text     = "הוסף תרופה ✓";
        txtMedName.Text        = string.Empty;
        txtMedPrice.Text       = string.Empty;
        txtMedStock.Text       = "0";
        lblMedError.IsVisible  = false;
        MedOverlay.IsVisible   = true;
    }

    private void OpenEditDialog(MedRow med)
    {
        _editingMedId          = med.Id;
        lblMedDialogTitle.Text = "עריכת תרופה";
        lblSaveMedBtn.Text     = "שמור שינויים ✓";
        txtMedName.Text        = med.Name;
        txtMedPrice.Text       = med.Price.ToString(CultureInfo.InvariantCulture);
        txtMedStock.Text       = (med.Stock ?? 0).ToString();
        lblMedError.IsVisible  = false;
        MedOverlay.IsVisible   = true;
    }

    private void CloseMedDialog_Click(object? sender, RoutedEventArgs e)
        => MedOverlay.IsVisible = false;

    private async void BtnSaveMed_Click(object? sender, RoutedEventArgs e)
    {
        var name = txtMedName.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            ShowMedError("יש להזין שם תרופה.");
            return;
        }
        if (!decimal.TryParse(txtMedPrice.Text, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out var price) || price < 0)
        {
            ShowMedError("יש להזין מחיר תקין (מספר חיובי).");
            return;
        }
        int.TryParse(txtMedStock.Text, out var stock);
        stock = Math.Max(0, stock);

        try
        {
            btnSaveMed.IsEnabled = false;

            if (_editingMedId == null)
            {
                // Insert new
                await SupabaseService.Client!.From<Medication>().Insert(new Medication
                {
                    Name  = name,
                    Price = price,
                    Stock = stock
                });
            }
            else
            {
                // Update existing
                await SupabaseService.Client!.From<Medication>()
                    .Where(m => m.Id == _editingMedId.Value)
                    .Set(m => m.Name!,  name)
                    .Set(m => m.Price,  price)
                    .Set(m => m.Stock,  (int?)stock)
                    .Update();
            }

            MedOverlay.IsVisible = false;
            await LoadDataAsync();
        }
        catch (Exception)
        {
            ShowMedError("שגיאה בשמירה, נסה שוב.");
        }
        finally
        {
            btnSaveMed.IsEnabled = true;
        }
    }

    private void ShowMedError(string msg)
    {
        lblMedError.Text      = "⚠ " + msg;
        lblMedError.IsVisible = true;
    }
}

// ── Display wrapper ────────────────────────────────────────────────────────
public class MedRow
{
    public int     Id         { get; }
    public string  Name       { get; }
    public decimal Price      { get; }
    public int?    Stock      { get; }
    public int     UsageCount { get; }

    public MedRow(Medication m, int usageCount)
    {
        Id         = m.Id;
        Name       = m.Name;
        Price      = m.Price;
        Stock      = m.Stock;
        UsageCount = usageCount;
    }

    // Returns (badgeBg, badgeFg, icon, label) based on stock level
    public (string Bg, string Fg, string Icon, string Text) StockStatus =>
        Stock switch
        {
            null    => ("#f1f5f9", "#94a3b8", "—",  "לא נמדד"),
            0       => ("#fee2e2", "#dc2626", "🔴", "אזל מהמלאי"),
            <= 5    => ("#fef9c3", "#a16207", "🟡", $"מלאי נמוך · {Stock}"),
            <= 20   => ("#dcfce7", "#15803d", "🟢", $"מלאי תקין · {Stock}"),
            _       => ("#dcfce7", "#15803d", "🟢", $"מלאי מלא · {Stock}")
        };
}
