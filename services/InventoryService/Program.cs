using System.Text.Json.Serialization;
using Contracts.Inventory;
using FluentValidation;
using InventoryService.Data;
using InventoryService.Services;
using InventoryService.Validators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "InventoryService",
        Version = "v1",
        Description = "Owns product stock. Responds to stock-check queries from OrdersService; consumes OrderPlaced events to decrement; publishes StockLow events."
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<IValidator<StockCheckRequest>, StockCheckRequestValidator>();
builder.Services.AddScoped<IInventoryService, InventoryBusinessService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.Migrate();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "InventoryService v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();

app.Run();

public partial class Program { }
