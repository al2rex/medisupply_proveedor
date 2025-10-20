using backend.Config;
using backend.Controllers;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar strongly-typed settings
builder.Services.Configure<PulsarSettings>(
    builder.Configuration.GetSection("PulsarSettings")
);

// Registrar como singleton directo
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<PulsarSettings>>().Value
);



builder.Services.AddSingleton<PulsarService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var pulsarService = app.Services.GetRequiredService<PulsarService>();
await pulsarService.SuscribirseASolicitudProveedorAsync();

app.MapPost("/inventario", async (PulsarService pulsar, string mensaje) =>
{
    await pulsar.PublicarInventarioRecibidoAsync(mensaje);
    return Results.Ok("Mensaje publicado");
});



app.Run();
