using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Firewall
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationId { get; set; }

    public decimal VendorId { get; set; }

    public string? Model { get; set; }

    public string? Firmware { get; set; }

    public string? Status { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}
