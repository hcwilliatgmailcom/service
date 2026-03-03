using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Operatingsystem
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal VendorId { get; set; }

    public string? Version { get; set; }

    public string? Architecture { get; set; }

    public DateTime? Endoflife { get; set; }

    public virtual ICollection<Server> Servers { get; set; } = new List<Server>();

    public virtual Vendor Vendor { get; set; } = null!;
}
