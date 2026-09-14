namespace Shed.Data
{
    public class UserHiddenSetting
    {
        public string UserId { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public string ThumbnailLogoUrl { get; set; } = string.Empty;
        public string FontStyle { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}


