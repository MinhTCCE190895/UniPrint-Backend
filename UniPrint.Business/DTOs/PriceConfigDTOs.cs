namespace UniPrint.Business.DTOs;

/// <summary>Task 20: Bảng giá in ấn hệ thống.</summary>
public class PriceConfigDto
{
    public Guid Id { get; set; }
    public decimal PricePerBWPage { get; set; }
    public decimal PricePerColorPage { get; set; }
    public decimal DoubleSidedDiscountPercent { get; set; }
    public decimal BindingPrice { get; set; }
    public decimal StaplePrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Task 20: Admin cập nhật bảng giá (giá trang màu, đen trắng, chiết khấu 2 mặt, gáy/ghim).</summary>
public class UpdatePriceConfigDto
{
    public decimal PricePerBWPage { get; set; }
    public decimal PricePerColorPage { get; set; }
    public decimal DoubleSidedDiscountPercent { get; set; }
    public decimal BindingPrice { get; set; }
    public decimal StaplePrice { get; set; }
}
