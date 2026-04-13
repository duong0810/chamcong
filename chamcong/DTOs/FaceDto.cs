using System.Text.Json.Serialization;

namespace chamcong.DTOs
{
    public class FaceCaptureResult
    {
        [JsonPropertyName("descriptor")]
        public double[] Descriptor { get; set; } = System.Array.Empty<double>();

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    public class FaceComparisonResult
    {
        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("similarity")]
        public double Similarity { get; set; }

        [JsonPropertyName("isMatch")]
        public bool IsMatch { get; set; }
    }
}