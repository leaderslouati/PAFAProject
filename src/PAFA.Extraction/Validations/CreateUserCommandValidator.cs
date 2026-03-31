using FluentValidation;
using PAFA.Extraction.Commands.Users;

namespace PAFA.Extraction.Validations;

/// <summary>
/// Validates the CreateUserCommand before it reaches the handler.
/// Rules map directly to the acceptance criteria:
///   - Mandatory fields, valid email format, valid role list.
/// </summary>
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    // Valid seeded role IDs (PafaUser=1, PafaAdmin=2, PacMember=3, Shipper=4)
    private static readonly IReadOnlyCollection<int> ValidRoleIds = [1, 2, 3, 4];

    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");

        RuleFor(x => x.JobTitle)
            .MaximumLength(150).WithMessage("Job title must not exceed 150 characters.")
            .When(x => x.JobTitle is not null);

        RuleFor(x => x.Department)
            .MaximumLength(150).WithMessage("Department must not exceed 150 characters.")
            .When(x => x.Department is not null);

        RuleFor(x => x.RoleIds)
            .NotNull().WithMessage("At least one role must be assigned.")
            .NotEmpty().WithMessage("At least one role must be assigned.");

        // Each submitted role id must match one of the 4 seeded roles
        RuleForEach(x => x.RoleIds)
            .Must(id => ValidRoleIds.Contains(id))
            .WithMessage("Role id '{PropertyValue}' is not a valid PAFA role.");
    }
}
