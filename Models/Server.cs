using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Server
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationId { get; set; }

    public decimal? RackId { get; set; }

    public decimal OperatingsystemId { get; set; }

    public decimal VendorId { get; set; }

    public decimal? Cpucores { get; set; }

    public decimal? Ramgb { get; set; }

    public string? Type { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();

    public virtual ICollection<Databaseinstance> Databaseinstances { get; set; } = new List<Databaseinstance>();

    public virtual Location Location { get; set; } = null!;

    public virtual Operatingsystem Operatingsystem { get; set; } = null!;

    public virtual Rack? Rack { get; set; }

    public virtual ICollection<ServerApplication> ServerApplications { get; set; } = new List<ServerApplication>();

    public virtual ICollection<ServerNetwork> ServerNetworks { get; set; } = new List<ServerNetwork>();

    public virtual Vendor Vendor { get; set; } = null!;
}
