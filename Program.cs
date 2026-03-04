using Microsoft.EntityFrameworkCore;
using service.Data;
using service.Infrastructure;
using service.Services;

var builder = WebApplication.CreateBuilder(args);

// Build entity metadata from the EF model
var metadataService = new EntityMetadataService();
builder.Services.AddSingleton(metadataService);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditHistoryInterceptor>();
builder.Services.AddDbContext<CmdbContext>((sp, options) =>
    options.UseOracle(builder.Configuration.GetConnectionString("CmdbContext"))
           .AddInterceptors(sp.GetRequiredService<AuditHistoryInterceptor>()));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<CountrySyncService>();
builder.Services.AddSingleton<HolidaySyncService>();
builder.Services.AddHostedService<RestApiSyncService>();

// Initialize metadata using a temporary DbContext
var tempOptions = new DbContextOptionsBuilder<CmdbContext>()
    .UseOracle(builder.Configuration.GetConnectionString("CmdbContext"))
    .Options;
using (var tempContext = new CmdbContext(tempOptions))
{
    var connStr = builder.Configuration.GetConnectionString("CmdbContext")!;

    var dbSetNameByType = typeof(CmdbContext).GetProperties()
        .Where(p => p.PropertyType.IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
        .ToDictionary(p => p.PropertyType.GetGenericArguments()[0], p => p.Name);

    var entities = tempContext.Model.GetEntityTypes()
        .Where(e => e.FindPrimaryKey() != null && dbSetNameByType.ContainsKey(e.ClrType))
        .Select(e => (
            Schema:    (e.GetSchema() ?? "CMDB").ToUpperInvariant(),
            TableName: (e.GetTableName() ?? e.ClrType.Name).ToUpperInvariant(),
            DbSetName: dbSetNameByType[e.ClrType]
        ));

    var entityConfig = TableCommentBootstrap.EnsureAndLoad(connStr, entities);
    metadataService.Build(tempContext, entityConfig);
}

// Add controllers with generic controller support
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new GenericControllerModelConvention(metadataService));
})
.ConfigureApplicationPartManager(manager =>
{
    manager.FeatureProviders.Add(new GenericControllerFeatureProvider(metadataService));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
