using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Certificatestatus
{
    public decimal Id { get; set; }

    public string? Certificatename { get; set; }

    public string? Domain { get; set; }

    public string? Issuer { get; set; }

    public DateTime? Expirydate { get; set; }

    public string? Type { get; set; }

    public string? Servername { get; set; }

    public decimal? Daysuntilexpiry { get; set; }

    public string? Expirystatus { get; set; }
}
