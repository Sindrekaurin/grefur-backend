namespace grefurBackend.Types
{
    // Hjelpeklasse for å holde på synkroniserte rader før de konverteres til ML.NET format
    public class MlDataRow
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, float> Features { get; set; } = new();
        public float Label { get; set; }
    }
}