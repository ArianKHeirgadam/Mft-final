using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mft.Application.Abstractions;
using Mft.Application.Contracts;
using Mft.Infrastructure.Persistence;
namespace Mft.Infrastructure;
public sealed class AuthService(AppDbContext db,IConfiguration config,ISmsSender sms):IAuthService {
 public async Task<LoginResponse?> LoginAsync(LoginRequest r,CancellationToken ct){
  var u=await db.Users.SingleOrDefaultAsync(x=>x.Username==r.Username&&x.IsActive,ct);
  if(u is null||!BCrypt.Net.BCrypt.Verify(r.Password,u.PasswordHash)) return null;
  if(u.Role==Mft.Domain.UserRole.Student && u.MustChangePassword && !string.IsNullOrWhiteSpace(u.Mobile))
   await sms.SendAsync(u.Mobile,"ورود شما ثبت شد. برای ادامه، باید رمز عبور موقت خود را تغییر دهید.",ct);
  var keyText=config["Jwt:Key"];
  if(string.IsNullOrWhiteSpace(keyText)||keyText.Length<32||keyText.StartsWith("CHANGE_THIS",StringComparison.Ordinal))
   throw new InvalidOperationException("JWT key is not configured securely. Set Jwt:Key to a random secret of at least 32 characters.");
  var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
  var claims=new[]{new Claim(JwtRegisteredClaimNames.Sub,u.Id.ToString()),new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Name,u.Username),new Claim(ClaimTypes.Role,u.Role.ToString()),new Claim("must_change_password",u.MustChangePassword.ToString().ToLowerInvariant())};
  var token=new JwtSecurityToken(
   issuer:config["Jwt:Issuer"],
   audience:config["Jwt:Audience"],
   claims:claims,
   expires:DateTime.UtcNow.AddHours(8),
   signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
  return new(new JwtSecurityTokenHandler().WriteToken(token),u.Id,u.FullName,u.Role,u.MustChangePassword);
 }
 public async Task ChangePasswordAsync(Guid userId,ChangePasswordRequest r,CancellationToken ct){
  if(string.IsNullOrWhiteSpace(r.NewPassword)||r.NewPassword.Length<8) throw new ArgumentException("New password must contain at least 8 characters.");
  var u=await db.Users.SingleOrDefaultAsync(x=>x.Id==userId&&x.IsActive,ct)??throw new KeyNotFoundException();
  if(!BCrypt.Net.BCrypt.Verify(r.CurrentPassword,u.PasswordHash)) throw new UnauthorizedAccessException("Current password is invalid.");
  if(r.CurrentPassword==r.NewPassword) throw new ArgumentException("New password must differ from current password.");
  u.PasswordHash=BCrypt.Net.BCrypt.HashPassword(r.NewPassword); u.MustChangePassword=false; await db.SaveChangesAsync(ct);
  if(!string.IsNullOrWhiteSpace(u.Mobile)) await sms.SendAsync(u.Mobile,"رمز عبور حساب شما با موفقیت تغییر کرد.",ct);
 }
}