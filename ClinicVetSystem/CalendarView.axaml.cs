using System;
using System.Collections.Generic;
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

public partial class CalendarView : UserControl
{
    // ── State ──────────────────────────────────────────────────────────────
    private DateTime _currentWeekStart;
    private int _selectedDayIndex; // 0 = Sun … 4 = Thu

    private List<CalendarAppointment> _appointments = new();
    private List<Customer> _allCustomers = new();
    private List<Pet>      _allPets      = new();
    private List<Staff>    _allVets      = new();

    /// <summary>Set to true before navigating here to auto-open the New Appointment dialog once data loads.</summary>
    public bool OpenDialogAfterLoad { get; set; }

    // ── Localisation ───────────────────────────────────────────────────────
    private static readonly string[] HebrewDayNames =
        { "ראשון", "שני", "שלישי", "רביעי", "חמישי" };

    private static readonly string[] HebrewMonths =
    {
        "ינואר", "פברואר", "מרץ", "אפריל", "מאי", "יוני",
        "יולי", "אוגוסט", "ספטמבר", "אוקטובר", "נובמבר", "דצמבר"
    };

    // ── Card colour palette ────────────────────────────────────────────────
    private static readonly (string Bg, string Fg)[] Palette =
    {
        ("#dcfce7", "#15803d"),  // green
        ("#dbeafe", "#1d4ed8"),  // blue
        ("#fef9c3", "#a16207"),  // amber
        ("#fce7f3", "#9d174d"),  // pink
        ("#ede9fe", "#6d28d9"),  // purple
    };

    // ── Constructor ────────────────────────────────────────────────────────
    public CalendarView()
    {
        InitializeComponent();

        var today = DateTime.Today;
        _currentWeekStart = today.AddDays(-(int)today.DayOfWeek); // back to Sunday
        _selectedDayIndex = Math.Clamp((int)today.DayOfWeek, 0, 4);

        // Populate time combo 08:00 – 18:00, half-hour steps
        var times = new List<string>();
        for (int h = 8; h <= 18; h++)
        {
            times.Add($"{h:D2}:00");
            if (h < 18) times.Add($"{h:D2}:30");
        }
        cbTime.ItemsSource = times;
        dpAppointmentDate.SelectedDate = DateTimeOffset.Now;

        // Show skeleton UI immediately, then load real data
        RenderView();
        _ = LoadDataAsync();
    }

    // ── DB Load ────────────────────────────────────────────────────────────
    private async Task LoadDataAsync()
    {
        try
        {
            // Fetch visits, pets, customers and vets in parallel
            var visitsTask    = SupabaseService.Client!.From<Visit>().Get();
            var petsTask      = SupabaseService.Client!.From<Pet>()
                                    .Order(p => p.Name, Postgrest.Constants.Ordering.Ascending)
                                    .Get();
            var customersTask = SupabaseService.Client!.From<Customer>()
                                    .Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending)
                                    .Get();
            var staffTask     = SupabaseService.Client!.From<Staff>().Get();

            await Task.WhenAll(visitsTask, petsTask, customersTask, staffTask);

            var visits    = visitsTask.Result.Models;
            _allPets      = petsTask.Result.Models;
            _allCustomers = customersTask.Result.Models;
            _allVets      = staffTask.Result.Models
                                .Where(s => s.Role == "וטרינר/ית")
                                .OrderBy(s => s.Username)
                                .ToList();

            // Join visits → pet → customer for display
            _appointments = visits.Select(v =>
            {
                var pet      = _allPets.FirstOrDefault(p => p.Id == v.PetId);
                var customer = _allCustomers.FirstOrDefault(c => c.Id == pet?.OwnerId);
                return new CalendarAppointment
                {
                    VisitId      = v.Id,
                    PetName      = pet?.Name      ?? "חיה לא ידועה",
                    PetType      = pet?.PetType   ?? "",
                    CustomerName = customer?.FullName ?? "לקוח לא ידוע",
                    Reason       = v.Reason,
                    DateTime     = v.VisitDate,
                    ColorIndex   = Math.Abs(v.Id) % Palette.Length
                };
            }).ToList();

            // Populate combos in the dialog
            cbCustomer.ItemsSource = _allCustomers;
            cbVet.ItemsSource      = _allVets;

            RenderView();

            // If caller requested dialog to open after load (e.g. sidebar button)
            if (OpenDialogAfterLoad)
            {
                OpenDialogAfterLoad = false;
                OpenNewAppointmentDialog();
            }
        }
        catch (Exception)
        {
            // Silently keep showing whatever is rendered
        }
    }

    // ── Render orchestration ───────────────────────────────────────────────
    private void RenderView()
    {
        UpdateWeekHeader();
        RenderDayCards();
        RenderAppointmentsList();
    }

    // ── Week header ────────────────────────────────────────────────────────
    private void UpdateWeekHeader()
    {
        var end = _currentWeekStart.AddDays(4);
        lblWeekRange.Text =
            $"{_currentWeekStart.Day} – {end.Day} {HebrewMonths[_currentWeekStart.Month - 1]} {_currentWeekStart.Year}";

        var today      = DateTime.Today;
        bool isCurrent = today >= _currentWeekStart && today < _currentWeekStart.AddDays(5);
        lblWeekNote.Text = isCurrent ? "השבוע הנוכחי" : "";

        int idx = Math.Clamp((int)today.DayOfWeek, 0, 4);
        lblDateSubtitle.Text =
            $"היום: יום {HebrewDayNames[idx]}, {today.Day} ב{HebrewMonths[today.Month - 1]} {today.Year}";
    }

    // ── Day cards ──────────────────────────────────────────────────────────
    private void RenderDayCards()
    {
        WeekDaysGrid.Children.Clear();

        for (int i = 0; i < 5; i++)
        {
            var day      = _currentWeekStart.AddDays(i);
            int count    = _appointments.Count(a => a.DateTime.Date == day.Date);
            bool sel     = i == _selectedDayIndex;
            bool isToday = day.Date == DateTime.Today;

            var card = BuildDayCard(HebrewDayNames[i], day, count, sel, isToday);
            var idx  = i;
            card.PointerPressed += (_, _) =>
            {
                _selectedDayIndex = idx;
                RenderDayCards();
                RenderAppointmentsList();
            };

            Grid.SetColumn(card, i);
            WeekDaysGrid.Children.Add(card);
        }
    }

    private static Border BuildDayCard(
        string dayName, DateTime date, int count, bool selected, bool isToday)
    {
        var bg       = selected ? Color.Parse("#006c49")
                     : isToday  ? Color.Parse("#f0fdf4")
                                : Color.Parse("#f8fafc");
        var mainText = selected ? Colors.White          : Color.Parse("#1e293b");
        var subText  = selected ? Color.Parse("#a7f3d0") : Color.Parse("#64748b");
        var dotColor = selected ? Colors.White          : Color.Parse("#10b981");
        var emptyDot = selected ? Color.Parse("#4ade80") : Color.Parse("#e2e8f0");

        var dots = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        int dotCount = count > 0 ? Math.Min(count, 4) : 1;
        for (int d = 0; d < dotCount; d++)
            dots.Children.Add(new Border
            {
                Width = 7, Height = 7,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(count > 0 ? dotColor : emptyDot)
            });

        var content = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(new TextBlock
        {
            Text = dayName,
            FontSize = 10, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(subText),
            HorizontalAlignment = HorizontalAlignment.Center,
            LetterSpacing = 0.5
        });
        content.Children.Add(new TextBlock
        {
            Text = date.Day.ToString(),
            FontSize = 28, FontWeight = FontWeight.Black,
            Foreground = new SolidColorBrush(mainText),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new Border
        {
            Height = 4, Child = dots, Margin = new Thickness(0, 4)
        });
        content.Children.Add(new TextBlock
        {
            Text = count > 0 ? $"{count} פגישות" : "פנוי",
            FontSize = 11,
            Foreground = new SolidColorBrush(subText),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return new Border
        {
            Background   = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(20),
            Padding      = new Thickness(12, 18),
            BoxShadow    = selected
                ? BoxShadows.Parse("0 8 20 0 #33006c49")
                : BoxShadows.Parse("0 2 8 0 #08005236"),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child  = content
        };
    }

    // ── Appointment list ───────────────────────────────────────────────────
    private void RenderAppointmentsList()
    {
        var day      = _currentWeekStart.AddDays(_selectedDayIndex);
        var dayAppts = _appointments
            .Where(a => a.DateTime.Date == day.Date)
            .OrderBy(a => a.DateTime)
            .ToList();

        lblSelectedDayTitle.Text =
            $"יום {HebrewDayNames[_selectedDayIndex]}, {day.Day} ב{HebrewMonths[day.Month - 1]}";
        lblSelectedDayCount.Text = dayAppts.Count > 0
            ? $"{dayAppts.Count} פגישות מתוכננות"
            : "אין פגישות מתוכננות";
        lblTodayBadge.IsVisible = day.Date == DateTime.Today;

        AppointmentsList.Children.Clear();

        if (dayAppts.Count == 0)
        {
            AppointmentsList.Children.Add(BuildEmptyState());
            return;
        }

        foreach (var appt in dayAppts)
            AppointmentsList.Children.Add(BuildAppointmentCard(appt));
    }

    private static Control BuildEmptyState() =>
        new Border
        {
            Padding = new Thickness(0, 48),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "📅", FontSize = 52,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "אין פגישות ביום זה",
                        FontSize = 16, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "לחץ על '+ פגישה חדשה' כדי לקבוע",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };

    private Border BuildAppointmentCard(CalendarAppointment appt)
    {
        var (bgHex, fgHex) = Palette[appt.ColorIndex % Palette.Length];
        var bg     = Color.Parse(bgHex);
        var fg     = Color.Parse(fgHex);
        var fgMute = Color.FromArgb(160, fg.R, fg.G, fg.B);
        var badge  = Color.FromArgb(35,  fg.R, fg.G, fg.B);

        string emoji = appt.PetType switch
        {
            "כלב"  => "🐕",
            "חתול" => "🐈",
            "ציפור"=> "🐦",
            "זוחל" => "🦎",
            _      => "🐾"
        };

        // Left: time
        var timeCol = new StackPanel { Width = 72, VerticalAlignment = VerticalAlignment.Center };
        timeCol.Children.Add(new TextBlock
        {
            Text = appt.DateTime.ToString("HH:mm"),
            FontSize = 22, FontWeight = FontWeight.Black,
            Foreground = new SolidColorBrush(fg)
        });

        // Accent bar
        var bar = new Border
        {
            Width = 3, CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(fg),
            Margin = new Thickness(8, 4, 16, 4)
        };

        // Pet name + type badge
        var nameRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameRow.Children.Add(new TextBlock
        {
            Text = $"{emoji} {appt.PetName}",
            FontSize = 16, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(fg),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (!string.IsNullOrEmpty(appt.PetType))
            nameRow.Children.Add(new Border
            {
                Background = new SolidColorBrush(badge),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3),
                Child = new TextBlock
                {
                    Text = appt.PetType, FontSize = 11, FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(fg)
                }
            });

        var details = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(nameRow);
        details.Children.Add(new TextBlock
        {
            Text = $"👤 {appt.CustomerName}", FontSize = 13,
            Foreground = new SolidColorBrush(fgMute)
        });
        if (!string.IsNullOrWhiteSpace(appt.Reason))
            details.Children.Add(new TextBlock
            {
                Text = appt.Reason, FontSize = 13,
                Foreground = new SolidColorBrush(fgMute),
                TextWrapping = TextWrapping.Wrap
            });

        // Cancel button
        var cancelBtn = new Button
        {
            Content     = "🗑",
            Background  = new SolidColorBrush(Color.Parse("#fee2e2")),
            Foreground  = new SolidColorBrush(Color.Parse("#dc2626")),
            CornerRadius = new CornerRadius(10),
            Padding     = new Thickness(10, 6),
            FontSize    = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor      = new Cursor(StandardCursorType.Hand)
        };
        var visitId = appt.VisitId;
        cancelBtn.Click += async (_, _) => await CancelAppointmentAsync(visitId);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, Auto, *, Auto"), ColumnSpacing = 4 };
        Grid.SetColumn(timeCol,    0);
        Grid.SetColumn(bar,        1);
        Grid.SetColumn(details,    2);
        Grid.SetColumn(cancelBtn,  3);
        row.Children.Add(timeCol);
        row.Children.Add(bar);
        row.Children.Add(details);
        row.Children.Add(cancelBtn);

        return new Border
        {
            Background   = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(20),
            Padding      = new Thickness(20, 16),
            Child        = row
        };
    }

    // ── Cancel appointment ─────────────────────────────────────────────────
    private async Task CancelAppointmentAsync(int visitId)
    {
        try
        {
            await SupabaseService.Client!.From<Visit>()
                .Where(v => v.Id == visitId)
                .Delete();
            await LoadDataAsync();
        }
        catch (Exception) { /* silently ignore */ }
    }

    // ── Week navigation ────────────────────────────────────────────────────
    private void BtnPrevWeek_Click(object? sender, RoutedEventArgs e)
    {
        _currentWeekStart = _currentWeekStart.AddDays(-7);
        RenderView();
    }

    private void BtnNextWeek_Click(object? sender, RoutedEventArgs e)
    {
        _currentWeekStart = _currentWeekStart.AddDays(7);
        RenderView();
    }

    // ── Dialog: open / close ───────────────────────────────────────────────
    private void BtnNewAppointment_Click(object? sender, RoutedEventArgs e)
        => OpenNewAppointmentDialog();

    /// <summary>Opens the New Appointment dialog and resets all fields.</summary>
    public void OpenNewAppointmentDialog()
    {
        cbCustomer.SelectedIndex       = -1;
        cbPet.SelectedIndex            = -1;
        cbPet.IsEnabled                = false;
        cbPet.PlaceholderText          = "בחר קודם לקוח...";
        cbVet.SelectedIndex            = -1;
        cbTime.SelectedIndex           = -1;
        dpAppointmentDate.SelectedDate = DateTimeOffset.Now;
        lblDialogStatus.IsVisible      = false;
        NewAppointmentOverlay.IsVisible = true;
    }

    private void CloseDialog_Click(object? sender, RoutedEventArgs e)
        => NewAppointmentOverlay.IsVisible = false;

    // ── Dialog: customer → filter pets ────────────────────────────────────
    private void CbCustomer_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cbCustomer.SelectedItem is Customer selected)
        {
            var customerPets = _allPets.Where(p => p.OwnerId == selected.Id).ToList();
            cbPet.ItemsSource    = customerPets;
            cbPet.IsEnabled      = customerPets.Count > 0;
            cbPet.PlaceholderText = customerPets.Count > 0
                ? "בחר חיה..."
                : "ללקוח זה אין חיות רשומות";
            cbPet.SelectedIndex  = -1;
        }
        else
        {
            cbPet.ItemsSource    = null;
            cbPet.IsEnabled      = false;
            cbPet.PlaceholderText = "בחר קודם לקוח...";
        }
    }

    // ── Dialog: save ──────────────────────────────────────────────────────
    private async void BtnSaveAppointment_Click(object? sender, RoutedEventArgs e)
    {
        // Validate all required fields
        if (cbCustomer.SelectedItem is not Customer ||
            cbPet.SelectedItem      is not Pet selectedPet ||
            cbVet.SelectedItem      is not Staff selectedVet ||
            cbTime.SelectedItem     is null ||
            dpAppointmentDate.SelectedDate is null)
        {
            lblDialogStatus.Text      = "⚠ אנא מלא את כל השדות (כולל וטרינר מטפל)";
            lblDialogStatus.Foreground = new SolidColorBrush(Color.Parse("#ba1a1a"));
            lblDialogStatus.IsVisible  = true;
            return;
        }

        // Build DateTime from date + time combo
        var date   = dpAppointmentDate.SelectedDate!.Value.Date;
        var parts  = cbTime.SelectedItem!.ToString()!.Split(':');
        var apptDt = date.AddHours(int.Parse(parts[0])).AddMinutes(int.Parse(parts[1]));

        // ── Conflict check: no two appointments within 10 minutes ──
        bool hasConflict = _appointments.Any(
            a => Math.Abs((a.DateTime - apptDt).TotalMinutes) < 10);

        if (hasConflict)
        {
            lblDialogStatus.Text      = "⚠ קיים תור באותה שעה. יש לבחור שעה עם פער של לפחות 10 דקות";
            lblDialogStatus.Foreground = new SolidColorBrush(Color.Parse("#ba1a1a"));
            lblDialogStatus.IsVisible  = true;
            return;
        }

        var newVisit = new Visit
        {
            PetId     = selectedPet.Id,
            VisitDate = apptDt,
            Reason    = "טרם נקבע — ימולא על ידי הוטרינר",
            VetId     = selectedVet.Id,   // vet assigned at booking time
            BaseCost  = 0,
            TotalCost = 0
        };

        try
        {
            btnSaveAppointment.IsEnabled = false;
            lblDialogStatus.Text         = "שומר...";
            lblDialogStatus.Foreground   = new SolidColorBrush(Color.Parse("#64748b"));
            lblDialogStatus.IsVisible    = true;

            await SupabaseService.Client!.From<Visit>().Insert(newVisit);

            NewAppointmentOverlay.IsVisible = false;

            // Jump to the new appointment's day if it falls in the current week
            int offset = (int)(apptDt.Date - _currentWeekStart).TotalDays;
            if (offset is >= 0 and <= 4)
                _selectedDayIndex = offset;

            await LoadDataAsync(); // reload everything from DB
        }
        catch (Exception)
        {
            lblDialogStatus.Text      = "שגיאה בשמירה, נסה שוב";
            lblDialogStatus.Foreground = new SolidColorBrush(Color.Parse("#ba1a1a"));
            lblDialogStatus.IsVisible  = true;
        }
        finally
        {
            btnSaveAppointment.IsEnabled = true;
        }
    }
}

// ── Display model for calendar (joins Visit + Pet + Customer) ─────────────
public class CalendarAppointment
{
    public int      VisitId      { get; set; }
    public string   PetName      { get; set; } = "";
    public string   PetType      { get; set; } = "";
    public string   CustomerName { get; set; } = "";
    public string   Reason       { get; set; } = "";
    public DateTime DateTime     { get; set; }
    public int      ColorIndex   { get; set; }
}
