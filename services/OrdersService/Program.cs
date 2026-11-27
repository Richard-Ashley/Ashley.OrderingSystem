using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using OrdersService.Clients;
using OrdersService.Consumers;
using OrdersService.Data;
using OrdersService.Models;
using OrdersService.Services;
using OrdersService.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "OrdersService",
        Version = "v1",
        Description = "Receives orders, checks stock with InventoryService, publishes OrderPlaced events."
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<IValidator<PlaceOrderRequest>, PlaceOrderRequestValidator>();

builder.Services.AddHttpClient<IInventoryClient, HttpInventoryClient>(client =>
{
    var baseUrl = builder.Configuration["Inventory:BaseUrl"]
        ?? throw new InvalidOperationException("Inventory:BaseUrl is not configured.");
    var timeoutSeconds = builder.Configuration.GetValue("Inventory:HttpTimeoutSeconds", 5);

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StockLowConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbit = builder.Configuration.GetSection("RabbitMq");
        cfg.Host(
            rabbit["Host"] ?? "localhost",
            rabbit.GetValue<ushort>("Port", 5672),
            "/",
            h =>
            {
                h.Username(rabbit["User"] ?? "guest");
                h.Password(rabbit["Password"] ?? "guest");
            });

        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
        cfg.ConfigureEndpoints(context);
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrdersService v1");
        options.RoutePrefix = "swagger";
    });

    // Apply migrations on startup in Development so a fresh clone runs with no manual step.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.Migrate();
}

app.MapControllers();

app.Run();

public partial class Program { }
