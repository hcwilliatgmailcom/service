using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Networkmap
{
    public decimal Id { get; set; }

    public string? Networkname { get; set; }

    public decimal? Vlan { get; set; }

    public string? Subnet { get; set; }

    public string? Gateway { get; set; }

    public string? Networktype { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }
}
