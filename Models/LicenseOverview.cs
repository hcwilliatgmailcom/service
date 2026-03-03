using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Licenseoverview
{
    public decimal Id { get; set; }

    public string? Licensename { get; set; }

    public string? Productkey { get; set; }

    public DateTime? Expirydate { get; set; }

    public decimal? Seats { get; set; }

    public string? Type { get; set; }

    public string? Vendorname { get; set; }

    public decimal? Daysuntilexpiry { get; set; }
}
