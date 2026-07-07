using System.IO;

namespace Meduit.ShareNormalizer.Snowflake.Helpers
{
    internal sealed class FileValidator
    {
        private readonly Config _config;

        public FileValidator(Config config)
        {
            _config = config;
        }

        public ValidationResult Validate(
            string filePath)
        {
            ValidationResult result =
                new ValidationResult();

            if (!File.Exists(filePath))
            {
                result.IsValid = false;
                result.Message = "File not found.";

                return result;
            }

            FileInfo file =
                new FileInfo(filePath);

            if (file.Length <= 0)
            {
                result.IsValid = false;
                result.Message = "Empty file.";

                return result;
            }

            if (!_config.DataExtensions.Contains(
                    file.Extension.ToLower()))
            {
                result.IsValid = false;
                result.Message =
                    "Unsupported extension.";

                return result;
            }

            string pattern;

            bool hasDate =
                DatePatternValidator.TryGetPattern(
                    file.Name,
                    out pattern);

            if (!hasDate)
            {
                result.IsValid = false;

                result.Message =
                    "Date pattern not found.";

                result.DatePattern = "";

                return result;
            }

            result.IsValid = true;

            result.DatePattern = pattern;

            result.Message = "VALID";

            return result;
        }
    }

    internal sealed class ValidationResult
    {
        public bool IsValid;

        public string Message;

        public string DatePattern;
    }
}