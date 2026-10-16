using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Mft.Application.Abstractions;
using Mft.Application.Contracts;
namespace Mft.Api.Controllers;
[ApiController][Route("api/auth")]
public sealed class AuthController(IAuthService auth):ControllerBase {
 [AllowAnonymous][HttpPost("login")] public async Task<IActionResult> Login(LoginRequest r,CancellationToken ct){var x=await auth.LoginAsync(r,ct);return x is null?Unauthorized(new{message="نام کاربری یا رمز عبور صحیح نیست."}):Ok(x);}
 [Authorize][HttpPost("change-password")] public async Task<IActionResult> ChangePassword(ChangePasswordRequest r,CancellationToken ct){var id=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);await auth.ChangePasswordAsync(id,r,ct);return Ok(new{message="رمز عبور با موفقیت تغییر کرد."});}
}