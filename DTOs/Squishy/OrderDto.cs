using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? MainImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string OrderType { get; set; } = "POS";
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "BANK_TRANSFER_QR";
    public string PaymentStatus { get; set; } = "PENDING";
    public string OrderStatus { get; set; } = "PENDING";

    // Giao hàng
    public string? ShippingAddress { get; set; }
    public string? ShippingProvider { get; set; }
    public string? TrackingCode { get; set; }
    public decimal ShippingFee { get; set; }

    // Khâu chuẩn bị & đóng gói
    public string PreparationStatus { get; set; } = "PENDING";
    public string? PreparationNote { get; set; }
    public string? PackingChecklistJson { get; set; }

    public string? TransferContent { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentProofImageUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto> Details { get; set; } = new();
    public List<MediaItemDto> PaymentProofImages { get; set; } = new();
    public List<MediaItemDto> ShippingProofImages { get; set; } = new(); // Ảnh chụp lúc bàn giao shipper
}

public class CreateOrderItemDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CreateOrderRequest
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string OrderType { get; set; } = "POS"; // POS, DELIVERY, ONLINE
    public decimal DiscountAmount { get; set; }
    public string PaymentMethod { get; set; } = "BANK_TRANSFER_QR"; // CASH, BANK_TRANSFER_QR, COD
    public string? Note { get; set; }
    public string? PaymentProofImageUrl { get; set; }

    // Giao hàng
    public string? ShippingAddress { get; set; }
    public string? ShippingProvider { get; set; }
    public string? TrackingCode { get; set; }
    public decimal ShippingFee { get; set; }

    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class UpdateOrderStatusRequest
{
    public string Status { get; set; } = "COMPLETED"; // PENDING, COMPLETED, CANCELLED
    public string? PaymentStatus { get; set; } // PENDING, PAID, FAILED
}

public class UpdateFulfillmentRequest
{
    public string PreparationStatus { get; set; } = "PREPARING"; // PENDING, PREPARING, PACKED, SHIPPED
    public string? PreparationNote { get; set; }
    public string? TrackingCode { get; set; }
    public string? ShippingProvider { get; set; }
    public string? PackingChecklistJson { get; set; }
}
