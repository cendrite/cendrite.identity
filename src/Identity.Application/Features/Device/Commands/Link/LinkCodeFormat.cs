namespace Identity.Application.Features.Device.Commands.Link;

internal static class LinkCodeFormat
{
    public static string Normalize(string input) =>
        new(input.ToUpperInvariant()
            .Where(c => c is not ('-' or ' '))
            .Select(c => c switch { 'O' => '0', 'I' or 'L' => '1', _ => c })
            .ToArray());
}
