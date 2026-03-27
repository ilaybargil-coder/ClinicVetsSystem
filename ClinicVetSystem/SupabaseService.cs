using Supabase;
using System.Threading.Tasks;

namespace ClinicVetsSystem;

public static class SupabaseService
{
    private const string SupabaseUrl = "https://dbyykuyzgjfmsdtyrwzb.supabase.co";
    private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImRieXlrdXl6Z2pmbXNkdHlyd3piIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzQ1OTM1NzksImV4cCI6MjA5MDE2OTU3OX0.a8s8FzTJTsxVzCurhPyPG3xBQvu04Lbs3AHzuHwCw08";

    public static Client? Client { get; private set; }

    public static async Task InitializeAsync()
    {
        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false
        };

        Client = new Client(SupabaseUrl, SupabaseKey, options);
        await Client.InitializeAsync();
    }
}
