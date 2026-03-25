using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.SharedAccounts;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/shared-accounts")]
public class SharedAccountsController(AppDbContext dbContext, IAccountAccessService accountAccessService) : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = ["viewer", "editor", "owner"];

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<object>>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var ownedAccounts = await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        var accountIds = ownedAccounts.Select(x => x.Id).ToList();
        var memberRows = await dbContext.SharedAccountMembers
            .Where(x => accountIds.Contains(x.AccountId))
            .Join(
                dbContext.Users,
                member => member.UserId,
                memberUser => memberUser.Id,
                (member, memberUser) => new
                {
                    member.AccountId,
                    Response = new SharedAccountMemberResponse(
                        member.Id,
                        member.AccountId,
                        member.UserId,
                        memberUser.DisplayName,
                        memberUser.Email,
                        member.Role,
                        member.CreatedAt)
                })
            .ToListAsync(cancellationToken);

        var response = ownedAccounts.Select(account => new
        {
            accountId = account.Id,
            accountName = account.Name,
            owner = true,
            members = memberRows
                .Where(x => x.AccountId == account.Id)
                .Select(x => x.Response)
                .ToList()
        }).ToList();

        return Ok(new ApiResponse<IReadOnlyList<object>>(true, response));
    }

    [HttpPost("members")]
    public async Task<ActionResult<ApiResponse<SharedAccountMemberResponse>>> AddMember(SharedAccountMemberRequest request, CancellationToken cancellationToken)
    {
        var normalizedRole = request.Role.Trim().ToLowerInvariant();
        if (!AllowedRoles.Contains(normalizedRole))
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "Role is invalid."));
        }

        var ownerUserId = User.GetUserId();
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.UserId == ownerUserId, cancellationToken);
        if (account is null) return NotFound(new ApiResponse<string>(false, string.Empty, "Account not found."));

        var memberUser = await dbContext.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == request.Email.Trim().ToLower(), cancellationToken);
        if (memberUser is null)
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "That email does not belong to a registered user."));
        }

        if (memberUser.Id == ownerUserId)
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "The account owner already has access."));
        }

        var existing = await dbContext.SharedAccountMembers.SingleOrDefaultAsync(
            x => x.AccountId == request.AccountId && x.UserId == memberUser.Id,
            cancellationToken);

        if (existing is not null)
        {
            existing.Role = normalizedRole;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new ApiResponse<SharedAccountMemberResponse>(true, ToResponse(existing, memberUser)));
        }

        var member = new SharedAccountMember
        {
            AccountId = request.AccountId,
            UserId = memberUser.Id,
            AddedByUserId = ownerUserId,
            Role = normalizedRole,
        };

        dbContext.SharedAccountMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<SharedAccountMemberResponse>(true, ToResponse(member, memberUser)));
    }

    [HttpDelete("members/{id:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, CancellationToken cancellationToken)
    {
        var member = await dbContext.SharedAccountMembers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (member is null) return NotFound();

        var isOwner = await accountAccessService.IsOwnerAsync(User.GetUserId(), member.AccountId, cancellationToken);
        if (!isOwner)
        {
            return Forbid();
        }

        dbContext.SharedAccountMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static SharedAccountMemberResponse ToResponse(SharedAccountMember member, User user)
        => new(member.Id, member.AccountId, member.UserId, user.DisplayName, user.Email, member.Role, member.CreatedAt);
}
