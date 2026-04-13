using System.Text.Json.Serialization;

namespace chamcong.DTOs
{
    public class PermissionStatus
    {
        [JsonPropertyName("camera")]
        public string Camera { get; set; } = "";

        [JsonPropertyName("location")]
        public string Location { get; set; } = "";

        [JsonPropertyName("allGranted")]
        public bool AllGranted { get; set; }
    }

    public class PermissionRequestResult
    {
        [JsonPropertyName("camera")]
        public PermissionResult Camera { get; set; } = new PermissionResult();

        [JsonPropertyName("location")]
        public PermissionResult Location { get; set; } = new PermissionResult();

        [JsonPropertyName("allGranted")]
        public bool AllGranted { get; set; }
    }

    public class PermissionResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}