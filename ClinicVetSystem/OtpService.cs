using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ClinicVetsSystem;

public static class OtpService
{
    // ── Gmail SMTP Configuration ──────────────────────────────────────────────
    // 1. Use a Gmail address you own
    // 2. Go to: Google Account → Security → 2-Step Verification → App Passwords
    // 3. Generate an App Password and paste it below (not your regular password)
    private const string SenderEmail    = "YOUR_GMAIL@gmail.com";   // TODO: replace
    private const string SenderPassword = "YOUR_APP_PASSWORD";       // TODO: replace (16-char App Password)
    private const string SenderName     = "Clinic Vets System";

    private const int OtpExpiryMinutes = 10;

    // Key: lowercase email → (code, expiry)
    private static readonly Dictionary<string, (string Code, DateTime Expiry)> _store = new();

    // ── Send OTP ─────────────────────────────────────────────────────────────

    public static async Task SendOtpAsync(string toEmail, string purpose)
    {
        var code = GenerateAndStore(toEmail);

        bool isReset = purpose == "reset";

        var subject = isReset
            ? "Clinic Vets – Password Reset Code"
            : "Clinic Vets – Email Verification Code";

        var html = $"""
            <div style="font-family:Inter,sans-serif;max-width:480px;margin:0 auto;padding:32px;background:#f7f9fb;border-radius:16px;">
              <h2 style="color:#006c49;margin-bottom:8px;">Clinic Vets System</h2>
              <p style="color:#3c4a42;font-size:15px;">
                {(isReset ? "We received a request to reset your password." : "Please verify your email address.")}
              </p>
              <div style="background:#ffffff;border-radius:12px;padding:24px;text-align:center;margin:24px 0;box-shadow:0 4px 12px #0a00521a;">
                <p style="font-size:13px;color:#6c7a71;margin-bottom:8px;">YOUR VERIFICATION CODE</p>
                <p style="font-size:40px;font-weight:bold;letter-spacing:12px;color:#191c1e;margin:0;">{code}</p>
              </div>
              <p style="color:#6c7a71;font-size:13px;">
                This code expires in <strong>{OtpExpiryMinutes} minutes</strong>.<br/>
                If you did not request this, please ignore this email.
              </p>
            </div>
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(SenderName, SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(SenderEmail, SenderPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    // ── Verify OTP ────────────────────────────────────────────────────────────

    public static bool Verify(string email, string code)
    {
        var key = email.ToLower().Trim();
        if (!_store.TryGetValue(key, out var entry)) return false;

        if (DateTime.UtcNow > entry.Expiry)
        {
            _store.Remove(key);
            return false;
        }

        if (entry.Code != code.Trim()) return false;

        _store.Remove(key);
        return true;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static string GenerateAndStore(string email)
    {
        var code = new Random().Next(100000, 999999).ToString();
        _store[email.ToLower().Trim()] = (code, DateTime.UtcNow.AddMinutes(OtpExpiryMinutes));
        return code;
    }
}
