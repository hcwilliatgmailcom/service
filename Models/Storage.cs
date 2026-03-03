using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Storage
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationId { get; set; }

    public decimal VendorId { get; set; }

    public string? Type { get; set; }

    public decimal? Capacitytb { get; set; }

    public decimal? Usedtb { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}
