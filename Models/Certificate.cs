using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Certificate
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Domain { get; set; }

    public string? Issuer { get; set; }

    public DateTime? Expirydate { get; set; }

    public string? Type { get; set; }

    public decimal ServerId { get; set; }

    public virtual Server Server { get; set; } = null!;
}
