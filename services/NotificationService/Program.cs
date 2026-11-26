var builder = WebApplication.CreateBuilder(args);

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

app.MapControllers();

app.Run();

public partial class Program { }
