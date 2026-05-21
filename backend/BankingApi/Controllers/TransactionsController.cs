using System.Security.Claims;
using BankingApi.DTOs;
using BankingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private static readonly HashSet<string> StaffRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "teller",
        "manager",
        "admin"
    };

    private readonly TransactionService _transactionService;

    public TransactionsController(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpPost("transfer")]
    public async Task<ActionResult<TransactionResponse>> Transfer(TransferRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        try
        {
            var result = await _transactionService.ExecuteTransferAsync(
                request,
                userId,
                IsStaff(),
                HttpContext.Connection.RemoteIpAddress,
                Request.Headers.UserAgent.ToString());

            return Ok(result);
        }
        catch (TransferException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("deposit")]
    [Authorize(Policy = "TellerOrAbove")]
    public async Task<ActionResult<TransactionResponse>> Deposit(DepositRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        try
        {
            var result = await _transactionService.ExecuteDepositAsync(
                request,
                userId,
                HttpContext.Connection.RemoteIpAddress,
                Request.Headers.UserAgent.ToString());

            return Ok(result);
        }
        catch (TransferException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("withdrawal")]
    [Authorize(Policy = "TellerOrAbove")]
    public async Task<ActionResult<TransactionResponse>> Withdrawal(WithdrawalRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        try
        {
            var result = await _transactionService.ExecuteWithdrawalAsync(
                request,
                userId,
                HttpContext.Connection.RemoteIpAddress,
                Request.Headers.UserAgent.ToString());

            return Ok(result);
        }
        catch (TransferException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("history/{accountId:guid}")]
    public async Task<ActionResult<TransactionHistoryResponse>> GetHistory(
        Guid accountId,
        [FromQuery] TransactionHistoryQuery query)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        try
        {
            var result = await _transactionService.GetHistoryAsync(
                accountId,
                userId,
                IsStaff(),
                query);

            return Ok(result);
        }
        catch (TransferException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = default;

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }

    private bool IsStaff()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return role is not null && StaffRoles.Contains(role);
    }
}
