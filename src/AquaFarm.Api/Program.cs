using AquaFarm.Api.Models;
using AquaFarm.Api.Security;
using AquaFarm.Core.Interfaces;
using AquaFarm.Infrastructure.Data;
using AquaFarm.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AquaFarmDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=(localdb)\\mssqllocaldb;Database=AquaFarmDb;Trusted_Connection=True;"));

builder.Services.AddScoped<IFinancialCalculator, FinancialCalculator>();
builder.Services.AddScoped<ILoanService, LoanService>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddSingleton<SimpleTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
})
.AddScheme<AuthenticationSchemeOptions, SimpleBearerAuthenticationHandler>("Bearer", _ => { });

builder.Services.AddAuthorization();

var frontendUrl = builder.Configuration["App:FrontendUrl"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            policy.WithOrigins(frontendUrl).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

SeedData.Initialize(app.Services);

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapOpenApi();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AquaFarm API v1");
    options.RoutePrefix = "swagger";
});
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
