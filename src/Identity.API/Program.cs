using System.Reflection;
using Identity.API.Extensions;
using Identity.Domain.Constants;
using Identity.Domain.Extensions;
using Identity.Infrastructure.Extensions;
using Scalar.AspNetCore;
using Identity.Application.Settings;
using Identity.Application.Configuration;

var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;

var configuration = builder.Configuration;
configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddEnvironmentVariables();

if (env.IsDevelopment())
{
    configuration.AddJsonFile($"appsettings.{Environments.Development}.json", true, true);
    configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), true);
}

builder.AddLogging();
builder.Services.AddObservability();

builder.Services.AddDomain();

builder.Services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
builder.Services.AddOptions<SignupChallengeSettings>()
    .Bind(configuration.GetSection("SignupChallenge"))
    .Validate(s => s.Difficulty is >= 0 and <= SignupChallengeConstants.MaxDifficulty,
        $"SignupChallenge:Difficulty must be between 0 and {SignupChallengeConstants.MaxDifficulty}")
    .ValidateOnStart();
builder.Services.AddApplication(Assembly.Load("Identity.Application"));

builder.Services.AddInfrastructure(configuration, Assembly.Load("Identity.Infrastructure"));

builder.Services.AddAuthentication(configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowElectron", policy =>
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>();
        policy.WithOrigins(allowedOrigins!)
            .AllowAnyMethod()
            .AllowCredentials()
            .AllowAnyHeader();
    });
});

builder.Services.AddEndpoints();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Identity.API V1")
        .WithTheme(ScalarTheme.Purple)
        .HideDarkModeToggle()
        .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Axios);
});

if (app.Environment.IsProduction())
{
    app.Services.ApplyPendingMigrations();
}

app.UseHttpsRedirection();

app.UseCors("AllowElectron");

app.UseAuthentication();

app.UseAuthorization();

app.RegisterMinimalEndpoints();

app.Run();

public partial class Program { }
