using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Mft.Application.Abstractions;
using Mft.Application.Services;
using Mft.Infrastructure;
using Mft.Infrastructure.Persistence;
using Mft.Api;
using Mft.Domain;

var builder=WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlServer(
    builder.Configuration.GetConnectionString("Default"),
    sql=>sql.EnableRetryOnFailure(
        maxRetryCount:5,
        maxRetryDelay:TimeSpan.FromSeconds(10),
        errorNumbersToAdd:null)));

builder.Services.AddScoped<IMftDbContext>(sp=>sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IMftService,MftService>();
builder.Services.AddScoped<IAuthService,AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser,CurrentUser>();
builder.Services.AddHttpClient("sms");
builder.Services.AddScoped<ISmsSender,KavenegarSmsSender>();

var keyText=builder.Configuration["Jwt:Key"];
if(string.IsNullOrWhiteSpace(keyText)||keyText.Length<32||keyText.StartsWith("CHANGE_THIS",StringComparison.Ordinal))
    throw new InvalidOperationException("Jwt:Key is required and must be a random secret of at least 32 characters.");

var key=Encoding.UTF8.GetBytes(keyText);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o=>o.TokenValidationParameters=new TokenValidationParameters{
        ValidateIssuer=!string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]),
        ValidIssuer=builder.Configuration["Jwt:Issuer"],
        ValidateAudience=!string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]),
        ValidAudience=builder.Configuration["Jwt:Audience"],
        ValidateLifetime=true,
        ValidateIssuerSigningKey=true,
        IssuerSigningKey=new SymmetricSecurityKey(key)
    });

builder.Services.AddAuthorization();

var configuredCorsOrigins=builder.Configuration
    .GetSection("Cors:Origins")
    .GetChildren()
    .Select(x=>x.Value)
    .Where(x=>!string.IsNullOrWhiteSpace(x))
    .Cast<string>()
    .ToArray();

builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>p
    .SetIsOriginAllowed(origin =>
    {
        if(configuredCorsOrigins.Contains(origin,StringComparer.OrdinalIgnoreCase))
            return true;

        if(!builder.Environment.IsDevelopment())
            return false;

        if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri))
            return false;

        return uri.Scheme=="http" &&
               (uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("127.0.0.1",StringComparison.OrdinalIgnoreCase));
    })
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app=builder.Build();

// First-run administrator bootstrap. Credentials come from environment variables or user-secrets.
// Existing usernames are never overwritten; no default administrator password is embedded in source.
if (app.Configuration.GetValue<bool>("Bootstrap:CreateSuperAdmin"))
{
    var username = app.Configuration["Bootstrap:Username"]?.Trim();
    var password = app.Configuration["Bootstrap:Password"];
    var fullName = app.Configuration["Bootstrap:FullName"]?.Trim();
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || password.Length < 12 || string.IsNullOrWhiteSpace(fullName))
        throw new InvalidOperationException("Bootstrap requires Username, FullName, and a unique Password of at least 12 characters.");
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var existing = await db.Users.SingleOrDefaultAsync(u => u.Username == username);
    if (existing is null)
    {
        db.Users.Add(new User {
            Id = Guid.NewGuid(), Username = username, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.SuperAdmin, FullName = fullName, Email = app.Configuration["Bootstrap:Email"] ?? "",
            Mobile = app.Configuration["Bootstrap:Mobile"] ?? "", DepartmentId = null, IsActive = true,
            MustChangePassword = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        app.Logger.LogWarning("Bootstrap SuperAdmin {Username} was created. Disable Bootstrap:CreateSuperAdmin after confirming login.", username);
    }
    else if (existing.Role != UserRole.SuperAdmin)
        throw new InvalidOperationException("Bootstrap username already exists with a non-SuperAdmin role. Choose another username.");
}

app.UseExceptionHandler(handler=>handler.Run(async context=>{
    var ex=context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.ContentType="application/json";
    context.Response.StatusCode=ex switch{
        UnauthorizedAccessException=>StatusCodes.Status403Forbidden,
        ArgumentException=>StatusCodes.Status400BadRequest,
        KeyNotFoundException=>StatusCodes.Status404NotFound,
        _=>StatusCodes.Status500InternalServerError
    };
    await context.Response.WriteAsJsonAsync(new{
        message=app.Environment.IsDevelopment()?ex?.Message:"خطای داخلی سرور."
    });
}));

if(app.Environment.IsDevelopment()){
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        bool.TryParse(context.User.FindFirst("must_change_password")?.Value, out var mustChange) && mustChange &&
        context.Request.Path.StartsWithSegments("/api"))
    {
        var isChangePassword = HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase);
        var isIdentityCheck = HttpMethods.IsGet(context.Request.Method) &&
            context.Request.Path.Equals("/api/portal/me", StringComparison.OrdinalIgnoreCase);
        if (!isChangePassword && !isIdentityCheck)
        {
            var userIdText = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdText, out var userId))
            {
                await using var scope = context.RequestServices.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var stillMustChange = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (bool?)u.MustChangePassword).SingleOrDefaultAsync();
                if (stillMustChange == true)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "برای ادامه، ابتدا رمز موقت خود را تغییر دهید.", code = "PASSWORD_CHANGE_REQUIRED" });
                    return;
                }
            }
        }
    }
    await next();
});
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "Mft.Api" })).AllowAnonymous();
app.MapControllers();
app.Run();
