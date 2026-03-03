using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Contactdirectory
{
    public decimal Id { get; set; }

    public string? Contactname { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Role { get; set; }

    public string? Departmentname { get; set; }

    public string? Costcenter { get; set; }
}
