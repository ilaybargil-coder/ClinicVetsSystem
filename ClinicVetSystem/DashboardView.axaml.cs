using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    // ── Called by MainMenuWindow every time the Dashboard tab is shown ──────
    public async Task LoadDataAsync(Staff staff)
    {
        try
        {
            // Immediate greeting so screen doesn't look empty
            var hour = DateTime.Now.Hour;
            var greet = hour < 12 ? "בוקר טוב" : hour < 17 ? "צהריים טובים" : "ערב טוב";
            lblGreetingMain.Text = $"{greet}, {staff.Username}! ";
            lblTopName.Text = staff.Username;
            lblTopRole.Text = staff.Role?.ToUpper();
            lblDateLine.Text = "טוען נתונים...";

            // Parallel DB fetch
            var visitsTask    = SupabaseService.Client!.From<Visit>().Get();
            var petsTask      = SupabaseService.Client!.From<Pet>().Get();
            var customersTask = SupabaseService.Client!.From<Customer>().Get();
            var allStaffTask  = SupabaseService.Client!.From<Staff>().Get();
            var medsTask      = SupabaseService.Client!.From<Medication>().Get();

            await Task.WhenAll(visitsTask, petsTask, customersTask, allStaffTask, medsTask);

            var visits    = visitsTask.Result.Models;
            var pets      = petsTask.Result.Models;
            var customers = customersTask.Result.Models;
            var allStaff  = allStaffTask.Result.Models;
            var meds      = medsTask.Result.Models;

            var today       = DateTime.Today;
            var todayVisits = visits.Where(v => v.VisitDate.Date == today)
                                    .OrderBy(v => v.VisitDate).ToList();

            // Stats
            int total   = todayVisits.Count;
            int done    = todayVisits.Count(v => !string.IsNullOrWhiteSpace(v.Diagnosis));
            int pending = total - done;

            lblStatTotal.Text   = total.ToString();
            lblStatPending.Text = pending.ToString();
            lblStatDone.Text    = done.ToString();
            lblDateLine.Text    = $"היום: {today:dd/MM/yyyy}  ·  {(pending > 0 ? $"{pending} ממתינים לטיפול" : "כל המטופלים טופלו ✓")}";

            // Build appointment display items
            var appts = todayVisits.Select(v =>
            {
                var pet      = pets.FirstOrDefault(p => p.Id == v.PetId);
                var customer = customers.FirstOrDefault(c => c.Id == pet?.OwnerId);
                var vet      = allStaff.FirstOrDefault(s => s.Id == v.VetId);
                return new DashboardAppt
                {
                    VisitId      = v.Id,
                    Time         = v.VisitDate,
                    PetName      = pet?.Name      ?? "חיה לא ידועה",
                    PetType      = pet?.PetType   ?? "",
                    CustomerName = customer?.FullName ?? "לקוח לא ידוע",
                    VetName      = vet?.Username  ?? "לא שויך",
                    IsCompleted  = !string.IsNullOrWhiteSpace(v.Diagnosis)
                };
            }).ToList();

            RenderAppointments(appts);

            // Low stock medications (stock ≤ 10)
            var lowStock = meds.Where(m => m.Stock != null && m.Stock <= 10)
                               .OrderBy(m => m.Stock).Take(7).ToList();
            RenderLowStock(lowStock);
        }
        catch (Exception)
        {
            lblDateLine.Text = "⚠ שגיאה בטעינת נתונים";
        }
    }

    // ── Appointment cards ──────────────────────────────────────────────────
    private void RenderAppointments(List<DashboardAppt> appts)
    {
        DashboardAppsList.Children.Clear();

        if (appts.Count == 0)
        {
            var bitmap = new Bitmap(AssetLoader.Open(new Uri("avares://ClinicVetSystem/Assets/noAppointmentimg.png")));
            DashboardAppsList.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#F8FFFE")),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(48, 48, 48, 40),
                BoxShadow = BoxShadows.Parse("0 4 24 0 #08005236"),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 4,
                    Children =
                    {
                        new Image
                        {
                            Source = bitmap,
                            Width = 360,
                            Height = 320,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 12)
                        },
                        new TextBlock
                        {
                            Text = "הכל רגוע היום!",
                            FontSize = 26, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#1e293b")),
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "אין תורים מתוכננים להיום",
                            FontSize = 15, FontWeight = FontWeight.Medium,
                            Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            });
            return;
        }

        foreach (var appt in appts)
            DashboardAppsList.Children.Add(BuildApptCard(appt));
    }

    private static Border BuildApptCard(DashboardAppt appt)
    {
        bool isDone = appt.IsCompleted;
        string petImg = appt.PetType switch
        {
            "כלב"   => "avares://ClinicVetSystem/Assets/dog.png",
            "חתול"  => "avares://ClinicVetSystem/Assets/cat.png",
            "ציפור" => "avares://ClinicVetSystem/Assets/bird.png",
            "זוחל"  => "avares://ClinicVetSystem/Assets/reptile.png",
            _       => "avares://ClinicVetSystem/Assets/dog.png"
        };
        var (badgeBg, badgeFg, badgeText) = isDone
            ? ("#dcfce7", "#15803d", "✓ טופל")
            : ("#fef9c3", "#a16207", "ממתין");

        // Time column
        var timeCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MinWidth = 58 };
        timeCol.Children.Add(new TextBlock
        {
            Text = appt.Time.ToString("HH:mm"), FontSize = 22, FontWeight = FontWeight.Black,
            Foreground = new SolidColorBrush(Color.Parse("#191c1e")),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        timeCol.Children.Add(new TextBlock
        {
            Text = appt.Time.Hour < 12 ? "AM" : "PM", FontSize = 9, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
            HorizontalAlignment = HorizontalAlignment.Center, LetterSpacing = 1
        });

        // Pet avatar
        var avatarBitmap = new Bitmap(AssetLoader.Open(new Uri(petImg)));
        var avatar = new Border
        {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(28),
            Background = new SolidColorBrush(Color.Parse("#f1f5f9")),
            ClipToBounds = true,
            Child = new Image
            {
                Source = avatarBitmap,
                Width = 44, Height = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform
            }
        };

        // Info
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(new TextBlock
        {
            Text = appt.PetName, FontSize = 17, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#191c1e")),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (!string.IsNullOrEmpty(appt.PetType))
            nameRow.Children.Add(new TextBlock
            {
                Text = $"· {appt.PetType}", FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                VerticalAlignment = VerticalAlignment.Center
            });

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(nameRow);
        details.Children.Add(new TextBlock
        {
            Text = $"👤 {appt.CustomerName}   🩺 {appt.VetName}",
            FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#64748b"))
        });

        // Status badge
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.Parse(badgeBg)),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(badgeFg))
            }
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, Auto, *, Auto"),
            ColumnSpacing = 20
        };
        Grid.SetColumn(timeCol, 0);
        Grid.SetColumn(avatar, 1);
        Grid.SetColumn(details, 2);
        Grid.SetColumn(badge, 3);
        row.Children.Add(timeCol);
        row.Children.Add(avatar);
        row.Children.Add(details);
        row.Children.Add(badge);

        return new Border
        {
            Background   = new SolidColorBrush(Colors.White),
            CornerRadius = new CornerRadius(20),
            Padding      = new Thickness(24),
            BoxShadow    = BoxShadows.Parse("0 2 8 0 #08005236"),
            Child        = row
        };
    }

    // ── Low stock list ─────────────────────────────────────────────────────
    private void RenderLowStock(List<Medication> meds)
    {
        LowStockList.Children.Clear();

        if (meds.Count == 0)
        {
            LowStockList.Children.Add(new TextBlock
            {
                Text = "✅ כל המלאי תקין",
                FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#15803d"))
            });
            return;
        }

        foreach (var med in meds)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Margin = new Thickness(0, 2)
            };
            row.Children.Add(new TextBlock
            {
                Text = med.Name, FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#1e293b")),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            bool isZero = med.Stock == 0;
            var stockBadge = new Border
            {
                Background   = new SolidColorBrush(isZero ? Color.Parse("#fee2e2") : Color.Parse("#fef9c3")),
                CornerRadius = new CornerRadius(8),
                Padding      = new Thickness(8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = isZero ? "אזל!" : $"{med.Stock} יח׳",
                    FontSize = 11, FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(isZero ? Color.Parse("#dc2626") : Color.Parse("#a16207"))
                }
            };
            Grid.SetColumn(stockBadge, 1);
            row.Children.Add(stockBadge);
            LowStockList.Children.Add(row);
        }
    }
}

// ── Display model ──────────────────────────────────────────────────────────
public class DashboardAppt
{
    public int      VisitId      { get; set; }
    public DateTime Time         { get; set; }
    public string   PetName      { get; set; } = "";
    public string   PetType      { get; set; } = "";
    public string   CustomerName { get; set; } = "";
    public string   VetName      { get; set; } = "";
    public bool     IsCompleted  { get; set; }
}
