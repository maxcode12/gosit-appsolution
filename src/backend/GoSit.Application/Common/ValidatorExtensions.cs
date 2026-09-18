using FluentValidation;

namespace GoSit.Application.Common;

public static class ValidatorExtensions
{
    /// <summary>
    /// Runs the validator and throws <see cref="RequestValidationException"/> on failure,
    /// which the API maps to a 400 with per-field messages.
    /// </summary>
    public static async Task ValidateOrThrowAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        throw new RequestValidationException(errors);
    }
}
