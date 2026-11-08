using Cendrite.Domain.Common;
using FluentValidation;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public class GetDeviceChainQueryValidator : AbstractValidator<GetDeviceChainQuery>
{
    public GetDeviceChainQueryValidator()
    {
        RuleFor(x => x.ConnectionId)
            .NotEmpty()
            .WithMessage(Errors.General.ValueIsEmpty(nameof(GetDeviceChainQuery.ConnectionId)).Description);
    }
}
