using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using service.Models;

namespace service.Data;

public partial class CmdbContext : DbContext
{
    public CmdbContext()
    {
    }

    public CmdbContext(DbContextOptions<CmdbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Application> Applications { get; set; }

    public virtual DbSet<ApplicationDatabase> ApplicationDatabases { get; set; }

    public virtual DbSet<Applicationcatalog> Applicationcatalogs { get; set; }

    public virtual DbSet<Applicationdependency> Applicationdependencies { get; set; }

    public virtual DbSet<Capacityplanning> Capacityplannings { get; set; }

    public virtual DbSet<Certificate> Certificates { get; set; }

    public virtual DbSet<Certificatestatus> Certificatestatuses { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<ContactDepartment> ContactDepartments { get; set; }

    public virtual DbSet<Contactdirectory> Contactdirectories { get; set; }

    public virtual DbSet<Databaseinstance> Databaseinstances { get; set; }

    public virtual DbSet<Databaseoverview> Databaseoverviews { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Departmentcontact> Departmentcontacts { get; set; }

    public virtual DbSet<Models.Environment> Environments { get; set; }

    public virtual DbSet<Environmentsummary> Environmentsummaries { get; set; }

    public virtual DbSet<Expiringcertificate> Expiringcertificates { get; set; }

    public virtual DbSet<Firewall> Firewalls { get; set; }

    public virtual DbSet<Firewallinventory> Firewallinventories { get; set; }

    public virtual DbSet<License> Licenses { get; set; }

    public virtual DbSet<Licenseoverview> Licenseoverviews { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Network> Networks { get; set; }

    public virtual DbSet<Networkassignment> Networkassignments { get; set; }

    public virtual DbSet<Networkmap> Networkmaps { get; set; }

    public virtual DbSet<Operatingsystem> Operatingsystems { get; set; }

    public virtual DbSet<Rack> Racks { get; set; }

    public virtual DbSet<Rackutilization> Rackutilizations { get; set; }

    public virtual DbSet<Server> Servers { get; set; }

    public virtual DbSet<ServerApplication> ServerApplications { get; set; }

    public virtual DbSet<ServerNetwork> ServerNetworks { get; set; }

    public virtual DbSet<Serverapplication1> Serverapplications { get; set; }

    public virtual DbSet<Serverbylocation> Serverbylocations { get; set; }

    public virtual DbSet<Serveroverview> Serveroverviews { get; set; }

    public virtual DbSet<Storage> Storages { get; set; }

    public virtual DbSet<Storagesummary> Storagesummaries { get; set; }

    public virtual DbSet<Switch> Switches { get; set; }

    public virtual DbSet<Switchinventory> Switchinventories { get; set; }

    public virtual DbSet<Vendor> Vendors { get; set; }

    public virtual DbSet<Vendorasset> Vendorassets { get; set; }

    public virtual DbSet<Widget> Widgets { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<Publicholiday> Publicholidays { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasDefaultSchema("CMDB")
            .UseCollation("USING_NLS_COMP");

        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008717");

            entity.ToTable("APPLICATION");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Criticality)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CRITICALITY");
            entity.Property(e => e.EnvironmentId)
                .HasColumnType("NUMBER")
                .HasColumnName("ENVIRONMENT_ID");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Tier)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TIER");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VERSION");

            entity.HasOne(d => d.Environment).WithMany(p => p.Applications)
                .HasForeignKey(d => d.EnvironmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_APP_ENV");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Applications)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_APP_VENDOR");
        });

        modelBuilder.Entity<ApplicationDatabase>(entity =>
        {
            entity.HasKey(e => new { e.ApplicationId, e.DatabaseinstanceId }).HasName("PK_APPDB");

            entity.ToTable("APPLICATION_DATABASE");

            entity.Property(e => e.ApplicationId)
                .HasColumnType("NUMBER")
                .HasColumnName("APPLICATION_ID");
            entity.Property(e => e.DatabaseinstanceId)
                .HasColumnType("NUMBER")
                .HasColumnName("DATABASEINSTANCE_ID");
            entity.Property(e => e.Accesstype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ACCESSTYPE");

            entity.HasOne(d => d.Application).WithMany(p => p.ApplicationDatabases)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AD_APP");

            entity.HasOne(d => d.Databaseinstance).WithMany(p => p.ApplicationDatabases)
                .HasForeignKey(d => d.DatabaseinstanceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AD_DB");
        });

        modelBuilder.Entity<Applicationcatalog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008793");

            entity.ToTable("APPLICATIONCATALOG");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Applicationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("APPLICATIONNAME");
            entity.Property(e => e.Criticality)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CRITICALITY");
            entity.Property(e => e.Environmentname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ENVIRONMENTNAME");
            entity.Property(e => e.Stage)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STAGE");
            entity.Property(e => e.Tier)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TIER");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VERSION");
        });

        modelBuilder.Entity<Applicationdependency>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("APPLICATIONDEPENDENCIES");

            entity.Property(e => e.Accesstype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ACCESSTYPE");
            entity.Property(e => e.Applicationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("APPLICATIONNAME");
            entity.Property(e => e.Appversion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("APPVERSION");
            entity.Property(e => e.Criticality)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CRITICALITY");
            entity.Property(e => e.Databasename)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("DATABASENAME");
            entity.Property(e => e.Dbversion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("DBVERSION");
            entity.Property(e => e.Engine)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ENGINE");
            entity.Property(e => e.Sizegb)
                .HasColumnType("NUMBER")
                .HasColumnName("SIZEGB");
        });

        modelBuilder.Entity<Capacityplanning>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("CAPACITYPLANNING");

            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("COUNTRY");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Rackcount)
                .HasColumnType("NUMBER")
                .HasColumnName("RACKCOUNT");
            entity.Property(e => e.Servercount)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVERCOUNT");
            entity.Property(e => e.Totalcpucores)
                .HasColumnType("NUMBER")
                .HasColumnName("TOTALCPUCORES");
            entity.Property(e => e.Totalramgb)
                .HasColumnType("NUMBER")
                .HasColumnName("TOTALRAMGB");
            entity.Property(e => e.Totalstoragecapacitytb)
                .HasColumnType("NUMBER")
                .HasColumnName("TOTALSTORAGECAPACITYTB");
            entity.Property(e => e.Totalstorageusedtb)
                .HasColumnType("NUMBER")
                .HasColumnName("TOTALSTORAGEUSEDTB");
        });

        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008765");

            entity.ToTable("CERTIFICATE");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Domain)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("DOMAIN");
            entity.Property(e => e.Expirydate)
                .HasColumnType("DATE")
                .HasColumnName("EXPIRYDATE");
            entity.Property(e => e.Issuer)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ISSUER");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.ServerId)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVER_ID");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");

            entity.HasOne(d => d.Server).WithMany(p => p.Certificates)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CERT_SERVER");
        });

        modelBuilder.Entity<Certificatestatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008795");

            entity.ToTable("CERTIFICATESTATUS");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Certificatename)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CERTIFICATENAME");
            entity.Property(e => e.Daysuntilexpiry)
                .HasColumnType("NUMBER")
                .HasColumnName("DAYSUNTILEXPIRY");
            entity.Property(e => e.Domain)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("DOMAIN");
            entity.Property(e => e.Expirydate)
                .HasColumnType("DATE")
                .HasColumnName("EXPIRYDATE");
            entity.Property(e => e.Expirystatus)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasColumnName("EXPIRYSTATUS");
            entity.Property(e => e.Issuer)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ISSUER");
            entity.Property(e => e.Servername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SERVERNAME");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008723");

            entity.ToTable("CONTACT");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.DepartmentId)
                .HasColumnType("NUMBER")
                .HasColumnName("DEPARTMENT_ID");
            entity.Property(e => e.Email)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("EMAIL");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("PHONE");
            entity.Property(e => e.Role)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ROLE");

            entity.HasOne(d => d.Department).WithMany(p => p.Contacts)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CONTACT_DEPT");
        });

        modelBuilder.Entity<ContactDepartment>(entity =>
        {
            entity.HasKey(e => new { e.ContactId, e.DepartmentId }).HasName("PK_CONTACTDEPT");

            entity.ToTable("CONTACT_DEPARTMENT");

            entity.Property(e => e.ContactId)
                .HasColumnType("NUMBER")
                .HasColumnName("CONTACT_ID");
            entity.Property(e => e.DepartmentId)
                .HasColumnType("NUMBER")
                .HasColumnName("DEPARTMENT_ID");
            entity.Property(e => e.Role)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ROLE");
            entity.Property(e => e.Since)
                .HasColumnType("DATE")
                .HasColumnName("SINCE");

            entity.HasOne(d => d.Contact).WithMany(p => p.ContactDepartments)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CD_CONTACT");

            entity.HasOne(d => d.Department).WithMany(p => p.ContactDepartments)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CD_DEPT");
        });

        modelBuilder.Entity<Contactdirectory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008797");

            entity.ToTable("CONTACTDIRECTORY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Contactname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CONTACTNAME");
            entity.Property(e => e.Costcenter)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("COSTCENTER");
            entity.Property(e => e.Departmentname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("DEPARTMENTNAME");
            entity.Property(e => e.Email)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("EMAIL");
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("PHONE");
            entity.Property(e => e.Role)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ROLE");
        });

        modelBuilder.Entity<Databaseinstance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008770");

            entity.ToTable("DATABASEINSTANCE");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Engine)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ENGINE");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Port)
                .HasColumnType("NUMBER")
                .HasColumnName("PORT");
            entity.Property(e => e.ServerId)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVER_ID");
            entity.Property(e => e.Sizegb)
                .HasColumnType("NUMBER")
                .HasColumnName("SIZEGB");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VERSION");

            entity.HasOne(d => d.Server).WithMany(p => p.Databaseinstances)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DB_SERVER");
        });

        modelBuilder.Entity<Databaseoverview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008799");

            entity.ToTable("DATABASEOVERVIEW");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Databasename)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("DATABASENAME");
            entity.Property(e => e.Engine)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("ENGINE");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Port)
                .HasColumnType("NUMBER")
                .HasColumnName("PORT");
            entity.Property(e => e.Servername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SERVERNAME");
            entity.Property(e => e.Sizegb)
                .HasColumnType("NUMBER")
                .HasColumnName("SIZEGB");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VERSION");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008691");

            entity.ToTable("DEPARTMENT");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Costcenter)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("COSTCENTER");
            entity.Property(e => e.Floor)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("FLOOR");
            entity.Property(e => e.Manager)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MANAGER");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
        });

        modelBuilder.Entity<Departmentcontact>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("DEPARTMENTCONTACTS");

            entity.Property(e => e.Contactname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CONTACTNAME");
            entity.Property(e => e.Costcenter)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("COSTCENTER");
            entity.Property(e => e.Departmentid)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("DEPARTMENTID");
            entity.Property(e => e.Departmentname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("DEPARTMENTNAME");
            entity.Property(e => e.Email)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("EMAIL");
            entity.Property(e => e.Manager)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MANAGER");
            entity.Property(e => e.Role)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ROLE");
            entity.Property(e => e.Since)
                .HasColumnType("DATE")
                .HasColumnName("SINCE");
        });

        modelBuilder.Entity<Models.Environment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008697");

            entity.ToTable("ENVIRONMENT");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Description)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Owner)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("OWNER");
            entity.Property(e => e.Stage)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STAGE");
        });

        modelBuilder.Entity<Environmentsummary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008801");

            entity.ToTable("ENVIRONMENTSUMMARY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Applicationcount)
                .HasColumnType("NUMBER")
                .HasColumnName("APPLICATIONCOUNT");
            entity.Property(e => e.Description)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.Environmentname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ENVIRONMENTNAME");
            entity.Property(e => e.Owner)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("OWNER");
            entity.Property(e => e.Stage)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STAGE");
        });

        modelBuilder.Entity<Expiringcertificate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008803");

            entity.ToTable("EXPIRINGCERTIFICATES");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Certificatename)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CERTIFICATENAME");
            entity.Property(e => e.Daysuntilexpiry)
                .HasColumnType("NUMBER")
                .HasColumnName("DAYSUNTILEXPIRY");
            entity.Property(e => e.Domain)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("DOMAIN");
            entity.Property(e => e.Expirydate)
                .HasColumnType("DATE")
                .HasColumnName("EXPIRYDATE");
            entity.Property(e => e.Issuer)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ISSUER");
            entity.Property(e => e.Servername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SERVERNAME");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
        });

        modelBuilder.Entity<Firewall>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008751");

            entity.ToTable("FIREWALL");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Firmware)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("FIRMWARE");
            entity.Property(e => e.LocationId)
                .HasColumnType("NUMBER")
                .HasColumnName("LOCATION_ID");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MODEL");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");

            entity.HasOne(d => d.Location).WithMany(p => p.Firewalls)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FW_LOCATION");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Firewalls)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FW_VENDOR");
        });

        modelBuilder.Entity<Firewallinventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008805");

            entity.ToTable("FIREWALLINVENTORY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Firewallname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("FIREWALLNAME");
            entity.Property(e => e.Firmware)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("FIRMWARE");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MODEL");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
        });

        modelBuilder.Entity<License>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008728");

            entity.ToTable("LICENSE");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Expirydate)
                .HasColumnType("DATE")
                .HasColumnName("EXPIRYDATE");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Productkey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("PRODUCTKEY");
            entity.Property(e => e.Seats)
                .HasColumnType("NUMBER")
                .HasColumnName("SEATS");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Licenses)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LICENSE_VENDOR");
        });

        modelBuilder.Entity<Licenseoverview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008807");

            entity.ToTable("LICENSEOVERVIEW");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Daysuntilexpiry)
                .HasColumnType("NUMBER")
                .HasColumnName("DAYSUNTILEXPIRY");
            entity.Property(e => e.Expirydate)
                .HasColumnType("DATE")
                .HasColumnName("EXPIRYDATE");
            entity.Property(e => e.Licensename)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LICENSENAME");
            entity.Property(e => e.Productkey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("PRODUCTKEY");
            entity.Property(e => e.Seats)
                .HasColumnType("NUMBER")
                .HasColumnName("SEATS");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008688");

            entity.ToTable("LOCATION");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("ADDRESS");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("COUNTRY");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
        });

        modelBuilder.Entity<Network>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008711");

            entity.ToTable("NETWORK");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Gateway)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("GATEWAY");
            entity.Property(e => e.LocationId)
                .HasColumnType("NUMBER")
                .HasColumnName("LOCATION_ID");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Subnet)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SUBNET");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
            entity.Property(e => e.Vlan)
                .HasColumnType("NUMBER")
                .HasColumnName("VLAN");

            entity.HasOne(d => d.Location).WithMany(p => p.Networks)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NETWORK_LOCATION");
        });

        modelBuilder.Entity<Networkassignment>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("NETWORKASSIGNMENT");

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IPADDRESS");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Macaddress)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("MACADDRESS");
            entity.Property(e => e.Networkname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NETWORKNAME");
            entity.Property(e => e.Networktype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("NETWORKTYPE");
            entity.Property(e => e.Servername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SERVERNAME");
            entity.Property(e => e.Serverstatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SERVERSTATUS");
            entity.Property(e => e.Subnet)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SUBNET");
            entity.Property(e => e.Vlan)
                .HasColumnType("NUMBER")
                .HasColumnName("VLAN");
        });

        modelBuilder.Entity<Networkmap>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008809");

            entity.ToTable("NETWORKMAP");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("COUNTRY");
            entity.Property(e => e.Gateway)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("GATEWAY");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Networkname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NETWORKNAME");
            entity.Property(e => e.Networktype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("NETWORKTYPE");
            entity.Property(e => e.Subnet)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SUBNET");
            entity.Property(e => e.Vlan)
                .HasColumnType("NUMBER")
                .HasColumnName("VLAN");
        });

        modelBuilder.Entity<Operatingsystem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008706");

            entity.ToTable("OPERATINGSYSTEM");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Architecture)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("ARCHITECTURE");
            entity.Property(e => e.Endoflife)
                .HasColumnType("DATE")
                .HasColumnName("ENDOFLIFE");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VERSION");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Operatingsystems)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OS_VENDOR");
        });

        modelBuilder.Entity<Rack>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008701");

            entity.ToTable("RACK");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.LocationId)
                .HasColumnType("NUMBER")
                .HasColumnName("LOCATION_ID");
            entity.Property(e => e.Maxunits)
                .HasColumnType("NUMBER")
                .HasColumnName("MAXUNITS");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Position)
                .HasColumnType("NUMBER")
                .HasColumnName("POSITION");
            entity.Property(e => e.Row)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("ROW");

            entity.HasOne(d => d.Location).WithMany(p => p.Racks)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RACK_LOCATION");
        });

        modelBuilder.Entity<Rackutilization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008811");

            entity.ToTable("RACKUTILIZATION");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Maxunits)
                .HasColumnType("NUMBER")
                .HasColumnName("MAXUNITS");
            entity.Property(e => e.Position)
                .HasColumnType("NUMBER")
                .HasColumnName("POSITION");
            entity.Property(e => e.Rackname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("RACKNAME");
            entity.Property(e => e.Row)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("ROW");
            entity.Property(e => e.Servercount)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVERCOUNT");
        });

        modelBuilder.Entity<Server>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008735");

            entity.ToTable("SERVER");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Cpucores)
                .HasColumnType("NUMBER")
                .HasColumnName("CPUCORES");
            entity.Property(e => e.LocationId)
                .HasColumnType("NUMBER")
                .HasColumnName("LOCATION_ID");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.OperatingsystemId)
                .HasColumnType("NUMBER")
                .HasColumnName("OPERATINGSYSTEM_ID");
            entity.Property(e => e.RackId)
                .HasColumnType("NUMBER")
                .HasColumnName("RACK_ID");
            entity.Property(e => e.Ramgb)
                .HasColumnType("NUMBER")
                .HasColumnName("RAMGB");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");

            entity.HasOne(d => d.Location).WithMany(p => p.Servers)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SERVER_LOCATION");

            entity.HasOne(d => d.Operatingsystem).WithMany(p => p.Servers)
                .HasForeignKey(d => d.OperatingsystemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SERVER_OS");

            entity.HasOne(d => d.Rack).WithMany(p => p.Servers)
                .HasForeignKey(d => d.RackId)
                .HasConstraintName("FK_SERVER_RACK");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Servers)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SERVER_VENDOR");
        });

        modelBuilder.Entity<ServerApplication>(entity =>
        {
            entity.HasKey(e => new { e.ServerId, e.ApplicationId }).HasName("PK_SERVERAPP");

            entity.ToTable("SERVER_APPLICATION");

            entity.Property(e => e.ServerId)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVER_ID");
            entity.Property(e => e.ApplicationId)
                .HasColumnType("NUMBER")
                .HasColumnName("APPLICATION_ID");
            entity.Property(e => e.Installeddate)
                .HasColumnType("DATE")
                .HasColumnName("INSTALLEDDATE");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");

            entity.HasOne(d => d.Application).WithMany(p => p.ServerApplications)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SA_APP");

            entity.HasOne(d => d.Server).WithMany(p => p.ServerApplications)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SA_SERVER");
        });

        modelBuilder.Entity<ServerNetwork>(entity =>
        {
            entity.HasKey(e => new { e.ServerId, e.NetworkId }).HasName("PK_SERVERNET");

            entity.ToTable("SERVER_NETWORK");

            entity.Property(e => e.ServerId)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVER_ID");
            entity.Property(e => e.NetworkId)
                .HasColumnType("NUMBER")
                .HasColumnName("NETWORK_ID");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IPADDRESS");
            entity.Property(e => e.Macaddress)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("MACADDRESS");

            entity.HasOne(d => d.Network).WithMany(p => p.ServerNetworks)
                .HasForeignKey(d => d.NetworkId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SN_NETWORK");

            entity.HasOne(d => d.Server).WithMany(p => p.ServerNetworks)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SN_SERVER");
        });

        modelBuilder.Entity<Serverapplication1>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("SERVERAPPLICATIONS");

            entity.Property(e => e.Applicationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("APPLICATIONNAME");
            entity.Property(e => e.Criticality)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CRITICALITY");
            entity.Property(e => e.Installeddate)
                .HasColumnType("DATE")
                .HasColumnName("INSTALLEDDATE");
            entity.Property(e => e.Installstatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("INSTALLSTATUS");
            entity.Property(e => e.Servername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SERVERNAME");
            entity.Property(e => e.Serverstatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SERVERSTATUS");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VERSION");
        });

        modelBuilder.Entity<Serverbylocation>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("SERVERBYLOCATION");

            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("COUNTRY");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Locationtype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("LOCATIONTYPE");
            entity.Property(e => e.Servercount)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVERCOUNT");
            entity.Property(e => e.Totalcpucores)
                .HasColumnType("NUMBER")
                .HasColumnName("TOTALCPUCORES");
            entity.Property(e => e.Totalramgb)
                .HasColumnType("NUMBER")
                .HasColumnName("TOTALRAMGB");
        });

        modelBuilder.Entity<Serveroverview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008813");

            entity.ToTable("SERVEROVERVIEW");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("COUNTRY");
            entity.Property(e => e.Cpucores)
                .HasColumnType("NUMBER")
                .HasColumnName("CPUCORES");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Operatingsystem)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("OPERATINGSYSTEM");
            entity.Property(e => e.Osversion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("OSVERSION");
            entity.Property(e => e.Ramgb)
                .HasColumnType("NUMBER")
                .HasColumnName("RAMGB");
            entity.Property(e => e.Servername)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SERVERNAME");
            entity.Property(e => e.Servertype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SERVERTYPE");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STATUS");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
        });

        modelBuilder.Entity<Storage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008744");

            entity.ToTable("STORAGE");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Capacitytb)
                .HasColumnType("NUMBER(10,2)")
                .HasColumnName("CAPACITYTB");
            entity.Property(e => e.LocationId)
                .HasColumnType("NUMBER")
                .HasColumnName("LOCATION_ID");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("TYPE");
            entity.Property(e => e.Usedtb)
                .HasColumnType("NUMBER(10,2)")
                .HasColumnName("USEDTB");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");

            entity.HasOne(d => d.Location).WithMany(p => p.Storages)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STORAGE_LOCATION");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Storages)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STORAGE_VENDOR");
        });

        modelBuilder.Entity<Storagesummary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008815");

            entity.ToTable("STORAGESUMMARY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Capacitytb)
                .HasColumnType("NUMBER(10,2)")
                .HasColumnName("CAPACITYTB");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Storagename)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("STORAGENAME");
            entity.Property(e => e.Storagetype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("STORAGETYPE");
            entity.Property(e => e.Usedpercent)
                .HasColumnType("NUMBER(5,1)")
                .HasColumnName("USEDPERCENT");
            entity.Property(e => e.Usedtb)
                .HasColumnType("NUMBER(10,2)")
                .HasColumnName("USEDTB");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
        });

        modelBuilder.Entity<Switch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008758");

            entity.ToTable("SWITCH");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.LocationId)
                .HasColumnType("NUMBER")
                .HasColumnName("LOCATION_ID");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MODEL");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Portcount)
                .HasColumnType("NUMBER")
                .HasColumnName("PORTCOUNT");
            entity.Property(e => e.RackId)
                .HasColumnType("NUMBER")
                .HasColumnName("RACK_ID");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");

            entity.HasOne(d => d.Location).WithMany(p => p.Switches)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SWITCH_LOCATION");

            entity.HasOne(d => d.Rack).WithMany(p => p.Switches)
                .HasForeignKey(d => d.RackId)
                .HasConstraintName("FK_SWITCH_RACK");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Switches)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SWITCH_VENDOR");
        });

        modelBuilder.Entity<Switchinventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008817");

            entity.ToTable("SWITCHINVENTORY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("CITY");
            entity.Property(e => e.Locationname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("LOCATIONNAME");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MODEL");
            entity.Property(e => e.Portcount)
                .HasColumnType("NUMBER")
                .HasColumnName("PORTCOUNT");
            entity.Property(e => e.Rackname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("RACKNAME");
            entity.Property(e => e.Switchname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SWITCHNAME");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008694");

            entity.ToTable("VENDOR");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Contracttype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CONTRACTTYPE");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Supportphone)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SUPPORTPHONE");
            entity.Property(e => e.Website)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("WEBSITE");
        });

        modelBuilder.Entity<Vendorasset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008819");

            entity.ToTable("VENDORASSETS");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Contracttype)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CONTRACTTYPE");
            entity.Property(e => e.Firewallcount)
                .HasColumnType("NUMBER")
                .HasColumnName("FIREWALLCOUNT");
            entity.Property(e => e.Licensecount)
                .HasColumnType("NUMBER")
                .HasColumnName("LICENSECOUNT");
            entity.Property(e => e.Oscount)
                .HasColumnType("NUMBER")
                .HasColumnName("OSCOUNT");
            entity.Property(e => e.Servercount)
                .HasColumnType("NUMBER")
                .HasColumnName("SERVERCOUNT");
            entity.Property(e => e.Storagecount)
                .HasColumnType("NUMBER")
                .HasColumnName("STORAGECOUNT");
            entity.Property(e => e.Switchcount)
                .HasColumnType("NUMBER")
                .HasColumnName("SWITCHCOUNT");
            entity.Property(e => e.Vendorname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("VENDORNAME");
        });

        modelBuilder.Entity<Widget>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SYS_C008823");

            entity.ToTable("WIDGET");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedDate)
                .HasColumnType("DATE")
                .HasColumnName("CREATED_DATE");
            entity.Property(e => e.Description)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("DESCRIPTION");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.VendorId)
                .HasColumnType("NUMBER")
                .HasColumnName("VENDOR_ID");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Widgets)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SYS_C008824");
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("COUNTRY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("CODE");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.Capital)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("CAPITAL");
            entity.Property(e => e.Region)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("REGION");
            entity.Property(e => e.Subregion)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("SUBREGION");
            entity.Property(e => e.Population)
                .HasColumnType("NUMBER")
                .HasColumnName("POPULATION");
            entity.Property(e => e.Area)
                .HasColumnType("NUMBER")
                .HasColumnName("AREA");
        });

        modelBuilder.Entity<Publicholiday>(entity =>
        {
            entity.ToTable("PUBLIC_HOLIDAY");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnType("NUMBER")
                .HasColumnName("ID");
            entity.Property(e => e.Date)
                .HasColumnType("DATE")
                .HasColumnName("DATE_");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("NAME");
            entity.Property(e => e.LocalName)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("LOCAL_NAME");
            entity.Property(e => e.Type)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("TYPE");
            entity.Property(e => e.CountryId)
                .HasColumnType("NUMBER")
                .HasColumnName("COUNTRY_ID");

            entity.HasOne(d => d.Country).WithMany(p => p.Publicholidays)
                .HasForeignKey(d => d.CountryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PH_COUNTRY");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
