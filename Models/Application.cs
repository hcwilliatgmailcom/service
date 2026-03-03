using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Application
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal VendorId { get; set; }

    public string? Version { get; set; }

    public string? Tier { get; set; }

    public string? Criticality { get; set; }

    public decimal EnvironmentId { get; set; }

    public virtual ICollection<ApplicationDatabase> ApplicationDatabases { get; set; } = new List<ApplicationDatabase>();

    public virtual Environment Environment { get; set; } = null!;

    public virtual ICollection<ServerApplication> ServerApplications { get; set; } = new List<ServerApplication>();

    public virtual Vendor Vendor { get; set; } = null!;
}
