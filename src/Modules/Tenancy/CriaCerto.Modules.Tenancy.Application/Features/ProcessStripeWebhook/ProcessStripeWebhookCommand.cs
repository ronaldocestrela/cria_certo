using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Features.ProcessStripeWebhook;

public sealed record ProcessStripeWebhookCommand(
    string JsonPayload,
    string StripeSignatureHeader
) : IRequest<Result<StripeWebhookResult>>;

public sealed class ProcessStripeWebhookCommandHandler : IRequestHandler<ProcessStripeWebhookCommand, Result<StripeWebhookResult>>
{
    private readonly IStripePaymentService _stripePaymentService;

    public ProcessStripeWebhookCommandHandler(IStripePaymentService stripePaymentService)
    {
        _stripePaymentService = stripePaymentService;
    }

    public async Task<Result<StripeWebhookResult>> Handle(ProcessStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        var result = await _stripePaymentService.ProcessWebhookAsync(
            request.JsonPayload,
            request.StripeSignatureHeader,
            cancellationToken);

        if (!result.Success)
        {
            return Result.Failure<StripeWebhookResult>(
                Error.Failure("Stripe.WebhookError", result.Message ?? "Erro ao processar evento do Stripe."));
        }

        return Result.Success(result);
    }
}
