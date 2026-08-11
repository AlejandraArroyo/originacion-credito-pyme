var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<Backend.Servicios.SolicitudRepositorio>();
builder.Services.AddScoped<Backend.Servicios.IndicadoresRepositorio>();
builder.Services.AddScoped<Backend.Servicios.PoliticaRepositorio>();
builder.Services.AddScoped<Backend.Servicios.DictamenRepositorio>();
builder.Services.AddScoped<Backend.Servicios.MetricasRepositorio>();
builder.Services.AddScoped<Backend.Servicios.AgenteFactory>();
builder.Services.AddScoped<Backend.Servicios.HerramientasAgente>();
builder.Services.AddScoped<Backend.Servicios.ObservabilidadRepositorio>();
builder.Services.AddScoped<Backend.Servicios.EvaluacionRepositorio>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthorization();
app.MapControllers();

app.Run();