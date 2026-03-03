using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Departmentcontact
{
    public decimal Departmentid { get; set; }

    public string Departmentname { get; set; } = null!;

    public string? Manager { get; set; }

    public string? Costcenter { get; set; }

    public string? Contactname { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; }

    public DateTime? Since { get; set; }
}
