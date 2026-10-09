using Serilog.Events;
using Serilog.Formatting;

namespace CrateEdit.Core.Diagnostics;

/// <summary>Formats a log event with an inner formatter, then redacts personal paths from the result.</summary>
internal sealed class RedactingFormatter(ITextFormatter inner, PathRedactor redactor) : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter();
        inner.Format(logEvent, buffer);
        output.Write(redactor.Redact(buffer.ToString()));
    }
}
