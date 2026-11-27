using MassTransit;
using NotificationService.Consumers;
using Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("NotificationService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "NotificationService",
        Version = "v1",
        Description = "Consumes OrderPlaced and StockLow events. In this demo, logs them (no real email delivery)."
    });
});

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();
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
        cfg.UseObservability(context, "notification");
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NotificationService v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseObservability();
app.MapControllers();

app.Run();

public partial class Program { }
