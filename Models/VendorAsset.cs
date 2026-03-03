using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Vendorasset
{
    public decimal Id { get; set; }

    public string? Vendorname { get; set; }

    public string? Contracttype { get; set; }

    public decimal? Servercount { get; set; }

    public decimal? Firewallcount { get; set; }

    public decimal? Switchcount { get; set; }

    public decimal? Storagecount { get; set; }

    public decimal? Licensecount { get; set; }

    public decimal? Oscount { get; set; }
}
