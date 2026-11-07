using Cendrite.Domain.Common;
using FluentValidation;
using Identity.Domain.Common;

namespace Identity.Application.Features.User.Queries.GetAccountKey;

public class GetAccountKeyQueryValidator : AbstractValidator<GetAccountKeyQuery>
{
    public GetAccountKeyQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(Errors.General.ValueIsEmpty(nameof(GetAccountKeyQuery.UserId)).Description);
    }
}
