using Azure.Identity;
using JIRAToModayAPI.Interfaces;
using JIRAToModayAPI.Models;
using JIRAToModayAPI.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;







// Add configuration sources
builder.Configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);

// Add Azure Key Vault configuration
var vaultUrl = builder.Configuration["KeyVault:VaultUrl"];
if (!string.IsNullOrEmpty(vaultUrl))
{
    builder.Configuration.AddAzureKeyVault(new Uri(vaultUrl), new DefaultAzureCredential());
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorPages();


//jwt stuff

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;

}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtConfig:Issuer"],
        ValidAudience = builder.Configuration["JwtConfig:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration["JwtConfig:Key"]))
    };

});


//JIRA Client
builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));

builder.Services.AddHttpClient<IJiraClient, JiraClient>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var baseUrl = configuration["Jira:Url"];
    if (!baseUrl.EndsWith("/"))
        baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    // Basic auth: username:apiToken (Base64)
    var username = configuration["Jira:Username"];
    var apiToken = configuration["JiraToken"]; // From Key Vault
    var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});


builder.Services.AddAuthorization();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IJwtService, JwtService>();
//add the blob storage service
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["BlobConnectionString"]; // This will come from Key Vault
    var containerName = configuration["AzureBlob:ContainerName"];
    var blobName = configuration["AzureBlob:BlobName"];
    return new JIRAToModayAPI.Storage.JiraMondayMappingService(connectionString, containerName,blobName);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/status", (IConfiguration cfg) => {
    var root = (IConfigurationRoot)cfg;
    const string probeKey = "AppName";
    var provider = root.Providers.Reverse().FirstOrDefault(p => p.TryGet(probeKey, out _));
    return Results.Json(new
    {
        appName = cfg[probeKey],
        keyVaultConnected = root.Providers.Any(p => p.GetType().Name.Contains("AzureKeyVault")),
        valueSource = provider?.ToString()
    });
});
app.Run();
