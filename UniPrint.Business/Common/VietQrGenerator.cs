using System.Globalization;
using System.Text;

namespace UniPrint.Business.Common;

/// <summary>
/// Task 18: Sinh nội dung mã VietQR (chuẩn EMVCo / NAPAS 247) để sinh viên quét bằng app ngân hàng.
/// Chuỗi payload được mobile/web vẽ thành QR, không phụ thuộc dịch vụ bên ngoài.
/// </summary>
public static class VietQrGenerator
{
    private const string NapasGuid = "A000000727";
    private const string TransferToAccountService = "QRIBFTTA";
    private const string CurrencyVnd = "704";
    private const string CountryVn = "VN";

    /// <summary>Nội dung chuyển khoản: mã đơn bỏ dấu "-" (VD: ORD20261028ABC123) để ngân hàng không cắt ký tự.</summary>
    public static string BuildTransferContent(string orderCode)
    {
        var builder = new StringBuilder();
        foreach (var ch in orderCode ?? string.Empty)
        {
            if (char.IsAsciiLetterOrDigit(ch)) builder.Append(char.ToUpperInvariant(ch));
        }
        var content = builder.ToString();
        return content.Length > 25 ? content[..25] : content;
    }

    /// <summary>Số tiền VND làm tròn về đồng (VietQR không nhận phần thập phân).</summary>
    public static long ToVndAmount(decimal amount)
        => (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static string BuildPayload(string bankBin, string accountNo, decimal amount, string transferContent)
    {
        var beneficiary = Tlv("00", bankBin) + Tlv("01", accountNo);
        var merchantAccount = Tlv("00", NapasGuid) + Tlv("01", beneficiary) + Tlv("02", TransferToAccountService);
        var vndAmount = ToVndAmount(amount);

        var payload = new StringBuilder()
            .Append(Tlv("00", "01"))
            .Append(Tlv("01", vndAmount > 0 ? "12" : "11"))
            .Append(Tlv("38", merchantAccount))
            .Append(Tlv("53", CurrencyVnd));
        if (vndAmount > 0) payload.Append(Tlv("54", vndAmount.ToString(CultureInfo.InvariantCulture)));
        payload.Append(Tlv("58", CountryVn));
        if (!string.IsNullOrEmpty(transferContent)) payload.Append(Tlv("62", Tlv("08", transferContent)));

        // CRC được tính trên toàn bộ chuỗi kể cả "6304".
        payload.Append("6304");
        var withCrcHeader = payload.ToString();
        return withCrcHeader + Crc16Ccitt(withCrcHeader).ToString("X4");
    }

    /// <summary>Ảnh QR dựng sẵn từ img.vietqr.io (dùng cho trang web Razor).</summary>
    public static string BuildImageUrl(VietQrSettings settings, decimal amount, string transferContent)
    {
        return $"https://img.vietqr.io/image/{settings.BankBin}-{settings.AccountNo}-compact2.png" +
               $"?amount={ToVndAmount(amount).ToString(CultureInfo.InvariantCulture)}" +
               $"&addInfo={Uri.EscapeDataString(transferContent)}" +
               $"&accountName={Uri.EscapeDataString(settings.AccountName)}";
    }

    public static ushort Crc16Ccitt(string input)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(input))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }
        return crc;
    }

    private static string Tlv(string id, string value)
        => id + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;
}
