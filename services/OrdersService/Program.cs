var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrdersService v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();

app.Run();

public partial class Program { }
