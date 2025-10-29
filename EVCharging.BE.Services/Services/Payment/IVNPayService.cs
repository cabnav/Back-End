using Microsoft.AspNetCore.Http;

public interface IVNPayService
{
    string BuildPayUrl(string code, decimal amount, string orderInfo, HttpRequest req);
    bool VerifySignature(IQueryCollection query, out Dictionary<string, string> data);
}
