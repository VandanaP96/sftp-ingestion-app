using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Meduit.ShareNormalizer.Snowflake.Helpers
{
    /// <summary>
    /// Validates whether a filename contains a recognizable date.
    ///
    /// Supported Examples
    /// ------------------
    /// 20250704
    /// 07042025
    /// 070425
    /// 2025-07-04
    /// 2025_07_04
    /// 07-04-2025
    /// 07-04-25
    /// 07.04.2025
    /// 07.04.25
    /// 7-4-2025
    /// 7.4.25
    /// 04_07_25
    /// 2025-0704
    /// 20250704_1945
    /// 2025_07_04_194510
    /// 07_04_25_19_45_10
    /// Jul2025
    /// July2025
    /// Jul 2025
    /// July 2025
    /// 2025Aug04
    /// 2025August04
    /// Aug042025
    /// August042025
    /// 04Aug2025
    /// 04August2025
    /// Jul-04-2025
    /// July-04-2025
    /// 2025.Jul.04
    /// 2025_Jul_04
    /// 07-2025
    /// 07.2025
    /// 07_2025
    /// 07/2025
    /// </summary>
    internal static class DatePatternValidator
    {
        private static readonly string[] Patterns =
        {
            //---------------------------------------------------------
            // Numeric Dates
            //---------------------------------------------------------

            @"(?<!\d)\d{8}(?!\d)",                        // YYYYMMDD / MMDDYYYY

            @"(?<!\d)\d{6}(?!\d)",                        // MMDDYY

            @"\b\d{4}-\d{2}-\d{2}\b",            // YYYY-MM-DD

            @"\b\d{4}_\d{2}_\d{2}\b",            // YYYY_MM_DD

            @"\b\d{4}/\d{2}/\d{2}\b",            // YYYY/MM/DD

            @"\b\d{2}-\d{2}-\d{4}\b",            // MM-DD-YYYY

            @"\b\d{2}/\d{2}/\d{4}\b",            // MM/DD/YYYY

            @"\b\d{2}\.\d{2}\.\d{4}\b",          // MM.DD.YYYY

            @"\b\d{2}_\d{2}_\d{4}\b",            // MM_DD_YYYY

            @"\b\d{2}-\d{2}-\d{2}\b",            // MM-DD-YY

            @"\b\d{2}/\d{2}/\d{2}\b",            // MM/DD/YY

            @"\b\d{2}\.\d{2}\.\d{2}\b",          // MM.DD.YY

            @"\b\d{2}_\d{2}_\d{2}\b",            // MM_DD_YY

            @"\b\d{1}-\d{1}-\d{4}\b",            // M-D-YYYY

            @"\b\d{1}/\d{1}/\d{4}\b",            // M/D/YYYY

            @"\b\d{1}\.\d{1}\.\d{4}\b",          // M.D.YYYY

            @"\b\d{1}-\d{1}-\d{2}\b",            // M-D-YY

            @"\b\d{1}/\d{1}/\d{2}\b",            // M/D/YY

            @"\b\d{1}\.\d{1}\.\d{2}\b",          // M.D.YY

            //---------------------------------------------------------
            // Month-Year only
            //---------------------------------------------------------

            @"\b\d{2}-\d{4}\b",                  // MM-YYYY

            @"\b\d{2}\.\d{4}\b",                 // MM.YYYY

            @"\b\d{2}_\d{4}\b",                  // MM_YYYY

            @"\b\d{2}/\d{4}\b",                  // MM/YYYY

            //---------------------------------------------------------
            // Timestamp formats
            //---------------------------------------------------------

            @"\b\d{8}_\d{4}\b",                  // YYYYMMDD_HHMM

            @"\b\d{8}_\d{6}\b",                  // YYYYMMDD_HHMMSS

            @"\b\d{4}_\d{2}_\d{2}_\d{6}\b",      // YYYY_MM_DD_HHMMSS

            @"\b\d{4}-\d{2}-\d{2}-\d{6}\b",      // YYYY-MM-DD-HHMMSS

            @"\b\d{2}_\d{2}_\d{2}_\d{2}_\d{2}_\d{2}\b", // MM_DD_YY_HH_MM_SS

            @"\b\d{14}\b",                       // YYYYMMDDHHMMSS

            //---------------------------------------------------------
            // Month Names
            //---------------------------------------------------------

            @"\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)\d{4}\b",

            @"\b(?:JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\d{4}\b",

            @"\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)\s\d{4}\b",

            @"\b(?:JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\s\d{4}\b",

            //---------------------------------------------------------
            // YYYYMonthDD
            //---------------------------------------------------------

            @"\b\d{4}(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)\d{2}\b",

            @"\b\d{4}(?:JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\d{2}\b",

            //---------------------------------------------------------
            // MonthDDYYYY
            //---------------------------------------------------------

            @"\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)\d{2}\d{4}\b",

            @"\b(?:JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\d{2}\d{4}\b",

            //---------------------------------------------------------
            // DDMonthYYYY
            //---------------------------------------------------------

            @"\b\d{2}(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)\d{4}\b",

            @"\b\d{2}(?:JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\d{4}\b",

            //---------------------------------------------------------
            // Mixed separators
            //---------------------------------------------------------

            @"\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)-\d{2}-\d{4}\b",

            @"\b(?:JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)-\d{2}-\d{4}\b",

            @"\b\d{4}\.(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)\.\d{2}\b",

            @"\b\d{4}_(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|SEPT|OCT|NOV|DEC)_\d{2}\b"
        };

        public static bool TryGetPattern(
    string fileName,
    out string matchedPattern)
        {
            matchedPattern = "";

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            foreach (string pattern in Patterns)
            {
                Match match =
                    Regex.Match(
                        fileName,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                string value =
                    match.Value;

                DateTime dt;

                if (DateTime.TryParseExact(
                        value,
                        new[]
                        {
                    "yyyyMMdd",
                    "MMddyyyy",
                    "MMddyy",
                    "yyyy-MM-dd",
                    "yyyy_MM_dd",
                    "yyyy/MM/dd",
                    "MM-dd-yyyy",
                    "MM/dd/yyyy",
                    "MM_dd_yyyy",
                    "MM.dd.yyyy"
                        },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out dt))
                {
                    matchedPattern = value;
                    return true;
                }

                // Month-name formats (April 2026, Jul2025, etc.)
                if (Regex.IsMatch(
                        value,
                        "[A-Za-z]",
                        RegexOptions.IgnoreCase))
                {
                    matchedPattern = value;
                    return true;
                }
            }

            return false;
        }
    }
}