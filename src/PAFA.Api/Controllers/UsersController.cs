using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.Users;
using System.Security.Claims;

namespace PAFA.Api.Controllers;

/// <summary>
/// User management endpoints � restricted to the PafaAdmin role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CanCreateUser")]
public class UsersController(
    IMediator mediator,
    IValidator<CreateUserCommand> validator) : ControllerBase
{
    /// <summary>
    /// Creates a new PAFA user account and assigns roles.
    /// </summary>
    /// <param name="request">User details and role assignment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created with the new user id.
    /// 400 Bad Request for validation errors.
    /// 409 Conflict if the email is already registered.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request, CancellationToken ct)
    {
        // Extract the admin's identity from the JWT � used for audit trail.
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? "unknown";

        // Map HTTP request ? MediatR command
        var command = new CreateUserCommand(
            FirstName:  request.FirstName,
            LastName:   request.LastName,
            Email:      request.Email,
            JobTitle:   request.JobTitle,
            Department: request.Department,
            RoleIds:    request.RoleIds,
            AdminId:    adminId);

        // Validate via FluentValidation before hitting the handler
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var result = await mediator.Send(command, ct);

        // Duplicate email ? 409 Conflict
        if (!result.Success && result.ErrorMessage?.Contains("already exists") == true)
            return Conflict(new ProblemDetails
            {
                Title  = "Duplicate email",
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status409Conflict
            });

        // Any other business failure ? 400
        if (!result.Success)
            return BadRequest(new ProblemDetails
            {
                Title  = "User creation failed",
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status400BadRequest
            });

        return CreatedAtAction(
            nameof(GetUserById),
            new { id = result.UserId },
            new CreateUserResponse(result.UserId!.Value));
    }

    /// <summary>
    /// Retrieves a PAFA user by their id.
    /// Used as the Location URL in the 201 Created response.
    /// </summary>
    /// <param name="id">User GUID.</param>
    [HttpGet("{id:guid}", Name = nameof(GetUserById))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetUserById(Guid id)
    {
        // Placeholder � implement GetUserByIdQuery when needed.
        return Ok(new { id });
    }
}

// ?? Inline DTOs ???????????????????????????????????????????????????????????

/// <summary>
/// Payload for POST /api/users.
/// </summary>
public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? JobTitle,
    string? Department,
    List<int> RoleIds
);

/// <summary>
/// Body returned on successful 201 Created.
/// </summary>
public record CreateUserResponse(Guid UserId);
