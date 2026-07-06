namespace sep490_be.Helpers
{
    public static class StringHelper
    {
        public static string GenerateTextSearch(params string?[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            var nonNullValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim());

            return string.Join(" ", nonNullValues);
        }
    }
}

