using Microsoft.Extensions.Configuration;

namespace UniPrint.Business.Common;

/// <summary>
/// Task 18: Thông tin tài khoản nhận tiền chuyển khoản VietQR của quầy in.
/// Đọc từ section "VietQR" trong appsettings.json (có giá trị mặc định để chạy demo).
/// </summary>
public class VietQrSettings
{
    /// <summary>Mã BIN ngân hàng theo chuẩn NAPAS (VD: 970436 = Vietcombank).</summary>
    public string BankBin { get; set; } = "970436";
    public string BankName { get; set; } = "Vietcombank";
    public string AccountNo { get; set; } = "1234567890";
    public string AccountName { get; set; } = "UNIPRINT FPTU";

    public static VietQrSettings FromConfiguration(IConfiguration? configuration)
    {
        var settings = new VietQrSettings();
        var section = configuration?.GetSection("VietQR");
        if (section == null) return settings;

        settings.BankBin = ValueOrDefault(section["BankBin"], settings.BankBin);
        settings.BankName = ValueOrDefault(section["BankName"], settings.BankName);
        settings.AccountNo = ValueOrDefault(section["AccountNo"], settings.AccountNo);
        settings.AccountName = ValueOrDefault(section["AccountName"], settings.AccountName);
        return settings;
    }

    private static string ValueOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
