using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Serveroverview
{
    public decimal Id { get; set; }

    public string? Servername { get; set; }

    public decimal? Cpucores { get; set; }

    public decimal? Ramgb { get; set; }

    public string? Servertype { get; set; }

    public string? Status { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Operatingsystem { get; set; }

    public string? Osversion { get; set; }

    public string? Vendorname { get; set; }
}
