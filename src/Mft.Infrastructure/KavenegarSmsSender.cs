using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Mft.Application.Abstractions;
namespace Mft.Infrastructure;
public sealed class KavenegarSmsSender(IConfiguration config,IHttpClientFactory clients):ISmsSender {
 public async Task SendAsync(string mobile,string message,CancellationToken ct) {
  if (!bool.TryParse(config["Sms:Enabled"],out var enabled) || !enabled) return;
  var key=config["Sms:ApiKey"]; var sender=config["Sms:Sender"];
  if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sender)) throw new InvalidOperationException("SMS provider is not configured.");
  var client=clients.CreateClient("sms");
  var url=$"https://api.kavenegar.com/v1/{Uri.EscapeDataString(key)}/sms/send.json?receptor={Uri.EscapeDataString(mobile)}&sender={Uri.EscapeDataString(sender)}&message={Uri.EscapeDataString(message)}";
  using var response=await client.GetAsync(url,ct);
  if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"SMS provider returned {(int)response.StatusCode}.");
 }
}
