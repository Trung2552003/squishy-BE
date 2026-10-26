using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class ReturnItemDto
{
    public Guid Id { get; set; }
    public Guid ReturnRequestId { get; set; }
    public Guid OrderDetailId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? MainImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ItemCondition { get; set; } = "DEFECTIVE";
    public Guid? ExchangeProductId { get; set; }
    public Guid? ExchangeVariantId { get; set; }
    public string? ExchangeProductName { get; set; }
    public int ExchangeQuantity { get; set; }
    public decimal ExchangeUnitPrice { get; set; }
}

public class ReturnRequestDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string RequestType { get; set; } = "RETURN";
    public string ReasonCategory { get; set; } = "DEFECTIVE";
    public string? Reason { get; set; }
    public string Status { get; set; } = "PENDING";
    public decimal RefundAmount { get; set; }
    public string? RefundMethod { get; set; }
    public decimal ExtraCharge { get; set; }
    public decimal RepairCost { get; set; }
    public bool StockRestored { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalQuantity { get; set; }
    public List<ReturnItemDto> Items { get; set; } = new();
    public List<MediaItemDto> Images { get; set; } = new();
}

/// <summary>Đơn hàng kèm số lượng còn có thể trả của từng món (dùng cho form tạo phiếu).</summary>
public class ReturnableOrderItemDto
{
    public Guid OrderDetailId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? MainImageUrl { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public int ReturnableQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ReturnableOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string OrderType { get; set; } = "POS";
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReturnableOrderItemDto> Items { get; set; } = new();
}

public class CreateReturnItemRequest
{
    public Guid OrderDetailId { get; set; }
    public int Quantity { get; set; }
    public string ItemCondition { get; set; } = "DEFECTIVE";
    public Guid? ExchangeProductId { get; set; }
    public Guid? ExchangeVariantId { get; set; }
    public int ExchangeQuantity { get; set; }
}

public class CreateReturnRequest
{
    public Guid OrderId { get; set; }
    public string RequestType { get; set; } = "RETURN";
    public string ReasonCategory { get; set; } = "DEFECTIVE";
    public string? Reason { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundMethod { get; set; }
    public decimal? ExtraCharge { get; set; }
    public decimal RepairCost { get; set; }
    public List<CreateReturnItemRequest> Items { get; set; } = new();
}

public class ReturnItemConditionUpdate
{
    public Guid Id { get; set; }
    public string ItemCondition { get; set; } = "DEFECTIVE";
}

public class UpdateReturnStatusRequest
{
    public string Status { get; set; } = "PROCESSING";
    public string? ResolutionNote { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundMethod { get; set; }
    public decimal? ExtraCharge { get; set; }
    public decimal? RepairCost { get; set; }
    public List<ReturnItemConditionUpdate>? Items { get; set; }
}
