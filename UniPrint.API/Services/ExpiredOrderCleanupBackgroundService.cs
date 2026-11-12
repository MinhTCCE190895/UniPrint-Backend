using UniPrint.Business.Services;

namespace UniPrint.API.Services;

/// <summary>
/// Background Service tự động quét và giải phóng các đơn hàng quá hạn mã QR nhận tài liệu đang nằm trên kệ.
/// Định kỳ chạy mỗi 60 giây.
/// </summary>
public class ExpiredOrderCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiredOrderCleanupBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

    public ExpiredOrderCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<ExpiredOrderCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredOrderCleanupBackgroundService đã khởi động (chu kỳ quét: 60s).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var shelfService = scope.ServiceProvider.GetRequiredService<IShelfService>();
                var result = await shelfService.CleanupExpiredShelfOrdersAsync(false, stoppingToken);
                if (result.Success && result.Data > 0)
                {
                    _logger.LogInformation("Đã tự động giải phóng {Count} đơn hàng quá hạn mã QR khỏi kệ lưu trữ.", result.Data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình tự động quét đơn quá hạn trên kệ.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

