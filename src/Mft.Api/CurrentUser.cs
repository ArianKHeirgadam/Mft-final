using System.Security.Claims;
using Mft.Application.Abstractions;
using Mft.Domain;
namespace Mft.Api;
public sealed class CurrentUser(IHttpContextAccessor http):ICurrentUser {
 public Guid UserId => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.HttpContext?.User.FindFirstValue("sub"),out var id)?id:Guid.Empty;
 public UserRole Role => Enum.TryParse<UserRole>(http.HttpContext?.User.FindFirstValue(ClaimTypes.Role),true,out var role)?role:UserRole.Student;
}
