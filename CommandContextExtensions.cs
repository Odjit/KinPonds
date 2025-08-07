using System;
using System.Collections.Generic;
using System.Text;
using VampireCommandFramework;

namespace KinPonds;
static class CommandContextExtensions
{
    internal static void PaginatedReply(this ICommandContext ctx, StringBuilder input) => PaginatedReply(ctx, input.ToString());

    const int MAX_MESSAGE_SIZE = 508 - 26 - 2 - 20; // factor for SysReply and newlines

    internal static void PaginatedReply(this ICommandContext ctx, string input)
    {
        if (input.Length <= MAX_MESSAGE_SIZE)
        {
            ctx.Reply(input);
            return;
        }

        var pages = SplitIntoPages(input);
        foreach (var page in pages)
        {
            var trimPage = page.TrimEnd('\n', '\r', ' ');
            trimPage = Environment.NewLine + trimPage;
            ctx.Reply(trimPage);
        }
    }

    /// <summary>
    /// This method splits <paramref name="rawText"/> into pages of <paramref name="pageSize"/> max size
    /// </summary>
    /// <param name="rawText"></param>
    internal static string[] SplitIntoPages(string rawText, int pageSize = MAX_MESSAGE_SIZE)
    {
        var pages = new List<string>();
        var page = new StringBuilder();
        var rawLines = rawText.Split(Environment.NewLine);
        var lines = new List<string>();

        // process rawLines -> lines of length <= pageSize
        foreach (var line in rawLines)
        {
            if (line.Length > pageSize)
            {
                // split into lines of max size preferring to split on spaces
                var remaining = line;
                while (!string.IsNullOrWhiteSpace(remaining) && remaining.Length > pageSize)
                {
                    // find the last space before the page size within 5% of pageSize buffer
                    var splitIndex = remaining.LastIndexOf(' ', pageSize - (int)(pageSize * 0.05));
                    if (splitIndex < 0)
                    {
                        splitIndex = Math.Min(pageSize - 1, remaining.Length);
                    }

                    lines.Add(remaining.Substring(0, splitIndex));
                    remaining = remaining.Substring(splitIndex);
                }
                lines.Add(remaining);
            }
            else
            {
                lines.Add(line);
            }
        }

        // batch as many lines together into pageSize
        foreach (var line in lines)
        {
            if ((page.Length + line.Length) > pageSize)
            {
                pages.Add(page.ToString());
                page.Clear();
            }
            page.AppendLine(line);
        }
        if (page.Length > 0)
        {
            pages.Add(page.ToString());
        }
        return pages.ToArray();
    }
}
