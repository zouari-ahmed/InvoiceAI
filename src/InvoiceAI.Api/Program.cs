using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---- 1. SERVICE REGISTRATION (the DI container) ----
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ---- 2. MIDDLEWARE PIPELINE (order matters here) ----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();