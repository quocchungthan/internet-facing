using System;
using System.Collections.Generic;
using System.Linq;

namespace OpensourceLab.FileStorage.ServedServices
{
    public sealed class PathWildcardScope
    {
        private readonly string[] _wildcards;

        public PathWildcardScope(IEnumerable<string> wildcards)
        {
            _wildcards = (wildcards ?? Array.Empty<string>())
                .Select(NormalizePath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> Wildcards => _wildcards;

        public bool IsAllowed(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            foreach (var wildcard in _wildcards)
            {
                if (wildcard is "/" or "/*")
                {
                    return true;
                }

                if (wildcard.EndsWith("/*", StringComparison.Ordinal))
                {
                    var prefix = wildcard[..^2].TrimEnd('/');
                    if (string.IsNullOrEmpty(prefix)
                        || normalizedPath.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                        || normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    continue;
                }

                if (normalizedPath.Equals(wildcard, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static PathWildcardScope FromDelimited(string wildcards)
        {
            var entries = string.IsNullOrWhiteSpace(wildcards)
                ? Array.Empty<string>()
                : wildcards.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new PathWildcardScope(entries);
        }

        public static string NormalizePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var value = raw.Replace('\\', '/').Trim();
            if (!value.StartsWith('/'))
            {
                value = "/" + value;
            }

            while (value.Contains("//", StringComparison.Ordinal))
            {
                value = value.Replace("//", "/", StringComparison.Ordinal);
            }

            return value;
        }
    }
}