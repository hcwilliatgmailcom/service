using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Applicationcatalog
{
    public decimal Id { get; set; }

    public string? Applicationname { get; set; }

    public string? Version { get; set; }

    public string? Tier { get; set; }

    public string? Criticality { get; set; }

    public string? Vendorname { get; set; }

    public string? Environmentname { get; set; }

    public string? Stage { get; set; }
}
