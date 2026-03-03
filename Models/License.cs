using System;
using System.Collections.Generic;

namespace service.Models;

public partial class License
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal VendorId { get; set; }

    public string? Productkey { get; set; }

    public DateTime? Expirydate { get; set; }

    public decimal? Seats { get; set; }

    public string? Type { get; set; }

    public virtual Vendor Vendor { get; set; } = null!;
}
