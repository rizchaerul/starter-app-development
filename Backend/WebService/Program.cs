using ClosedXML.Graphics;
using Database.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation.Processors.Security;
using OpenIddict.Abstractions;
using WebService.Contracts.Constants;
using WebService.Contracts.Options;
using WebService.Services;

var corsPolicy = "CorsPolicy";

// "Fallback font name" will likely be something like "DejaVu Sans" or "Tahoma"
// It is not a path to font file, but a font name.
// See: https://closedxml.readthedocs.io/en/latest/tips/missing-font.html
// ClosedXML.Excel.LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine("Carlito");

var builder = WebApplication.CreateBuilder(args);

// Heroku and Railway use PORT environment variable to store the port the we should be using.
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(int.Parse(port));
    });
}

var configuration = builder.Configuration;
var services = builder.Services;

// Get settings from appsettings.json
var openIdConnectOptions = configuration
    .GetSection(OpenIdConnectOptions.OpenIdConnect)
    .Get<OpenIdConnectOptions>();

// Register configuration in services
services.Configure<OpenIddictOptions>(configuration.GetSection(OpenIddictOptions.OpenIddict));

// Add services to the container.
services.AddControllers();
services.AddRazorPages();
services.AddHttpContextAccessor();

services.AddHostedService<OpeniddictClientInitializer>();

// Forwarded headers for reverse proxy, useful for https redirection on Heroku and Railway.
// Reference: https://stackoverflow.com/questions/43749236/net-core-x-forwarded-proto-not-working
services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
});

services.AddSwaggerDocument(options =>
{
    options.AddSecurity("bearer", new OpenApiSecurityScheme
    {
        AuthorizationUrl = $"{openIdConnectOptions?.Authority}/connect/authorize",
        TokenUrl = $"{openIdConnectOptions?.Authority}/connect/token",

        Flow = OpenApiOAuth2Flow.AccessCode,
        Type = OpenApiSecuritySchemeType.OAuth2,

        Scopes = new Dictionary<string, string>
        {
            { $"api", "Access APIs" },
        },
    });

    options.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));
});

services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicy, policy =>
    {
        policy.AllowAnyOrigin();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

// Add Database Context.
services.AddDbContextPool<ApplicationDbContext>(options =>
{
    // Set the default tracking behavior to no tracking.
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    options.UseNpgsql(configuration.GetConnectionString("Database"));
    options.UseOpenIddict();
});

services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/account/login";
    });

services.AddAuthorization(options =>
{
    options.AddPolicy(ApplicationConstants.AuthorizationPolicy.ApiScopePolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(OpenIddictConstants.Claims.Private.Scope, "api");
    });
});

services
    .AddOpenIddict()
    // Register the OpenIddict core components.
    .AddCore(options =>
    {
        // Configure OpenIddict to use the EF Core stores/models.
        options
            .UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>();
    })
    // Register the OpenIddict server components.
    .AddServer(options =>
    {
        // PKCE
        options
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();

        // Client Credentials
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        options.RegisterClaims(OpenIddictConstants.Claims.Email);

        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserinfoEndpointUris("/connect/userinfo")
            .SetLogoutEndpointUris("/connect/endsession");

        // Encryption and signing of tokens
        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate()
            .DisableAccessTokenEncryption();

        // Register scopes (permissions)
        options.RegisterScopes(OpenIddictConstants.Scopes.Email);
        options.RegisterScopes(OpenIddictConstants.Scopes.Profile);
        options.RegisterScopes("api");

        // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
        options
            .UseAspNetCore()
            .DisableTransportSecurityRequirement()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough()
            .EnableLogoutEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var app = builder.Build();

app.UseForwardedHeaders();

// Only use https redirection when ASPNETCORE_HTTPS_PORT environtment variable available.
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")))
{
    app.UseHttpsRedirection();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi3(options =>
    {
        options.OAuth2Client = new OAuth2ClientSettings
        {
            ClientId = openIdConnectOptions.ClientId,
            ClientSecret = null,
            UsePkceWithAuthorizationCodeGrant = true,
        };
    });
}
else if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.Run();
