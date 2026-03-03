using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Firewallinventory
{
    public decimal Id { get; set; }

    public string? Firewallname { get; set; }

    public string? Model { get; set; }

    public string? Firmware { get; set; }

    public string? Status { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }

    public string? Vendorname { get; set; }
}
