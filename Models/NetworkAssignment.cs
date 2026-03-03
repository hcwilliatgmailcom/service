using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Networkassignment
{
    public string Servername { get; set; } = null!;

    public string? Serverstatus { get; set; }

    public string Networkname { get; set; } = null!;

    public decimal? Vlan { get; set; }

    public string? Subnet { get; set; }

    public string? Networktype { get; set; }

    public string? Ipaddress { get; set; }

    public string? Macaddress { get; set; }

    public string Locationname { get; set; } = null!;
}
