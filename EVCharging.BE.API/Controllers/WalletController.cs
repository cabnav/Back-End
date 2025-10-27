using System.ComponentModel.DataAnnotations;
using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.AspNetCore.Mvc;
namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _wallet;
        public WalletController(IWalletService wallet) => _wallet = wallet;

        [HttpGet("balance/{userId:int}")]
        public async Task<IActionResult> GetBalance(int userId)
        {
            var bal = await _wallet.GetBalanceAsync(userId);
            return Ok(new { userId, balance = bal });
        }

        public class ListTxQuery
        {
            [Required] public int UserId { get; set; }
            public int Skip { get; set; } = 0;
            public int Take { get; set; } = 20;
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public string? Type { get; set; }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] ListTxQuery q)
        {
            var (items, total) = await _wallet.GetTransactionsAsync(q.UserId, q.Skip, q.Take, q.From, q.To, q.Type);
            var list = items.Select(t => new WalletTransactionDTO
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                Amount = t.Amount,
                TransactionType = t.TransactionType ?? "",
                Description = t.Description ?? "",
                BalanceAfter = t.BalanceAfter,
                ReferenceId = t.ReferenceId,
                CreatedAt = t.CreatedAt ?? DateTime.UtcNow
            });
            return Ok(new { total, items = list });
        }

        [HttpPost("credit")]
        public async Task<IActionResult> ManualCredit([FromBody] WalletCreditRequestDto req)
        {
            await _wallet.CreditAsync(req.UserId, req.Amount, req.Description ?? "Manual credit", req.ReferenceId);
            var bal = await _wallet.GetBalanceAsync(req.UserId);
            return Ok(new { message = "Credited", balance = bal });
        }

        [HttpPost("debit")]
        public async Task<IActionResult> ManualDebit([FromBody] WalletDebitRequestDto req)
        {
            await _wallet.DebitAsync(req.UserId, req.Amount, req.Description ?? "Manual debit", req.ReferenceId);
            var bal = await _wallet.GetBalanceAsync(req.UserId);
            return Ok(new { message = "Debited", balance = bal });
        }
    }
}
