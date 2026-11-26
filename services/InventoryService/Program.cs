var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
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
