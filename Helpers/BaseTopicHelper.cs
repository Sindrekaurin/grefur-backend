namespace grefurBackend.Helpers;

/* Helper class for handling topic-related operations in the application */
public static class TopicHelper
{
    /* Summary of function: Constructs full topic names following the pattern <DeviceId>/<BaseTopic>/<Suffix> */
    public static string ConstructTopic(string deviceId, string suffix, string baseTopic = "")
    {
        if (string.IsNullOrEmpty(baseTopic))
        {
            return $"{deviceId}/{suffix}";
        }

        return $"{deviceId}/{baseTopic}/{suffix}";
    }

    /* Summary of function: Generates a wildcard pattern following the pattern <DeviceId>/<BaseTopic>/# */
    public static string GetDeviceWildcard(string deviceId, string baseTopic = "")
    {
        if (string.IsNullOrEmpty(baseTopic))
        {
            return $"{deviceId}/#";
        }
        return $"{deviceId}/{baseTopic}/#";
    }

    /* Summary of function: Extracts the device ID from a topic string. DeviceId is now always assumed to be first index. */
    public static string ExtractDeviceId(string topic)
    {
        if (string.IsNullOrEmpty(topic)) return "unknown";
        var parts = topic.Split('/');

        return parts.Length > 0 ? parts[0] : "unknown";
    }
}