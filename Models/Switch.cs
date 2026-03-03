using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Switch
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationId { get; set; }

    public decimal? RackId { get; set; }

    public decimal VendorId { get; set; }

    public decimal? Portcount { get; set; }

    public string? Model { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual Rack? Rack { get; set; }

    public virtual Vendor Vendor { get; set; } = null!;
}
