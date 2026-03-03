using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Department
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Costcenter { get; set; }

    public string? Manager { get; set; }

    public string? Floor { get; set; }

    public virtual ICollection<ContactDepartment> ContactDepartments { get; set; } = new List<ContactDepartment>();

    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}
