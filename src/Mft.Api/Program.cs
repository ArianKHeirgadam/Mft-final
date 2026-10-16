using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Mft.Application.Abstractions;
using Mft.Application.Services;
using Mft.Infrastructure;
using Mft.Infrastructure.Persistence;
using Mft.Api;

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
app.UseAuthorization();
app.MapControllers();
app.Run();
