using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Switchinventory
{
    public decimal Id { get; set; }

    public string? Switchname { get; set; }

    public decimal? Portcount { get; set; }

    public string? Model { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }

    public string? Rackname { get; set; }

    public string? Vendorname { get; set; }
}
