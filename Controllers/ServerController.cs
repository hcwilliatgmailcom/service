// GET /api/{table} — sample REST source for sync
using Microsoft.AspNetCore.Mvc;

public class ServerController : Controller
{
    
    [HttpGet("/api/{controller}")]
    public IActionResult Api(string key)
    {
   
            var servers = new[]
            {
                new { name = "WEB-PROD-05", hostname = "web-prod-01.corp.local", ip_address = "10.0.1.10", os = "Ubuntu 24.04 LTS", cpu_cores = 8, ram_gb = 32, environment = "1", status = "Active" },
                new { name = "WEB-PROD-02", hostname = "web-prod-02.corp.local", ip_address = "10.0.1.11", os = "Ubuntu 24.04 LTS", cpu_cores = 8, ram_gb = 32, environment = "Production", status = "Active" },
                new { name = "DB-PROD-01", hostname = "db-prod-01.corp.local", ip_address = "10.0.2.10", os = "Red Hat 9.3", cpu_cores = 16, ram_gb = 128, environment = "Production", status = "Active" },
                new { name = "APP-STG-01", hostname = "app-stg-01.corp.local", ip_address = "10.0.3.10", os = "Windows Server 2022", cpu_cores = 4, ram_gb = 16, environment = "Staging", status = "Active" },
                new { name = "DEV-01", hostname = "dev-01.corp.local", ip_address = "10.0.4.10", os = "Ubuntu 22.04 LTS", cpu_cores = 4, ram_gb = 8, environment = "Development", status = "Maintenance" },
            };
            return Json(servers);
     

         
    }

}