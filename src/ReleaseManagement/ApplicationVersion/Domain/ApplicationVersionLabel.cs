namespace ReleaseManagement.Domain;

public readonly record struct ApplicationVersionLabel
{
    public ApplicationVersionLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "An Application Version label cannot be empty.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}
