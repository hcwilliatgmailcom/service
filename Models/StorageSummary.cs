using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Storagesummary
{
    public decimal Id { get; set; }

    public string? Storagename { get; set; }

    public string? Storagetype { get; set; }

    public decimal? Capacitytb { get; set; }

    public decimal? Usedtb { get; set; }

    public decimal? Usedpercent { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }

    public string? Vendorname { get; set; }
}
