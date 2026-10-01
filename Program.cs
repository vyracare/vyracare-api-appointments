using System.Text;
using System.Text.Json.Serialization;
using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Vyracare.Api.Appointments.Common.Configuration;
using Vyracare.Api.Appointments.Features.Appointments;
using Vyracare.Api.Appointments.Features.Appointments.Shared.Ports;
using Vyracare.Api.Appointments.Infrastructure;
using Vyracare.Api.Appointments.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
await ParameterStoreBootstrapper.ApplyAsync(builder.Configuration);
var configuration = builder.Configuration;

builder.Services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<AppointmentOptions>(configuration.GetSection(AppointmentOptions.SectionName));

builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return sp.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
});
builder.Services.AddScoped<IAppointmentRepository, MongoAppointmentRepository>();
builder.Services.AddScoped<AppointmentService>();

var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key)) throw new InvalidOperationException("Jwt:Key nao configurado.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
    };
});

var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
var origins = cors.AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("DefaultCors", policy =>
{
    if (origins.Length == 0 || origins.Contains("*")) policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "vyracare-api-appointments",
        Version = "v1",
        Description = "API de agenda e indicadores operacionais da Vyracare."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "vyracare-api-appointments" })).AllowAnonymous();
app.MapControllers().RequireAuthorization();
app.Run();

public partial class Program;
