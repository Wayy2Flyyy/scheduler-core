using FluentValidation;
using Shared;

namespace Coordinator.Validation;

public sealed class JobSubmissionRequestValidator : AbstractValidator<JobSubmissionRequest>
{
    public JobSubmissionRequestValidator()
    {
        RuleFor(request => request.Type)
            .NotEmpty()
            .Must(type => JobTypes.BuiltIn.Contains(type))
            .WithMessage("Unsupported job type.");

        RuleFor(request => request.Payload)
            .NotNull();

        RuleFor(request => request)
            .Must(request => request.RunAt is not null || !string.IsNullOrWhiteSpace(request.Cron))
            .WithMessage("Either runAt or cron must be provided.")
            .Must(request => request.RunAt is null || string.IsNullOrWhiteSpace(request.Cron))
            .WithMessage("Provide either runAt or cron, not both.");

        RuleFor(request => request.Cron)
            .Must(cron => string.IsNullOrWhiteSpace(cron) || cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 5)
            .WithMessage("Cron expression must have at least 5 fields.");

        RuleFor(request => request.Cron)
            .Custom((cron, context) =>
            {
                if (string.IsNullOrWhiteSpace(cron))
                {
                    return;
                }

                try
                {
                    CronSchedule.GetNextOccurrence(cron);
                }
                catch
                {
                    context.AddFailure("Cron", "Invalid cron expression.");
                }
            });

        RuleFor(request => request.MaxAttempts)
            .GreaterThan(0)
            .When(request => request.MaxAttempts.HasValue);

        RuleFor(request => request.TimeoutSeconds)
            .GreaterThan(0)
            .When(request => request.TimeoutSeconds.HasValue);
    }
}
