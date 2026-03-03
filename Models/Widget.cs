using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Widget
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal VendorId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual Vendor Vendor { get; set; } = null!;
}
