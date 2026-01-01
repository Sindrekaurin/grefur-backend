using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace grefurBackend.Models;

public enum SubscriptionLevel
{
    None = 0,
    Normal = 1,
    Premium = 2
}

public enum AlarmLevel
{
    None = 0,
    Basic = 1,
    Premium = 2
}

public enum NotificationTypes
{
    None = 0,
    SMS = 1,
    EMAIL = 2,
    SMS_EMAIL = 3,
    GREFUR_SMS_EMAIL = 4,
}

public record SubscriptionInfo(SubscriptionLevel Level, string Description);

public class GrefurCustomer
{
    public string CustomerId { get; set; } = string.Empty;
    public List<string> RegisteredDevices { get; set; } = new();

    // Default subscriptions
    public SubscriptionLevel LogSubscription { get; set; } = SubscriptionLevel.None;
    public AlarmLevel AlarmSubscription { get; set; } = AlarmLevel.None;
    public NotificationTypes NotificationTypes { get; set; } = NotificationTypes.None;

    /// <summary>
    /// Dynamically checks if the customer has any active subscription or notification type.
    /// This avoids manual updates when adding new subscription fields.
    /// </summary>
    public bool IsActiveSubscriber()
    {
        var Properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var Prop in Properties)
        {
            // Filters for properties related to subscriptions or notification types
            if (Prop.Name.EndsWith("Subscription") || Prop.Name.EndsWith("Types"))
            {
                var Value = Prop.GetValue(this);
                if (Value == null) continue;

                // Checks if the enum value is anything other than 'None' (0)
                if (Value.ToString() != "None" && (int)Value != 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Helper property to get subscription descriptions
    public string LogSubscriptionDescription => LogSubscription switch
    {
        SubscriptionLevel.None => "No logging allowed. Real-time monitoring only.",
        SubscriptionLevel.Normal => "Standard logging with 30 days retention.",
        SubscriptionLevel.Premium => "High-frequency logging with 1 year retention and advanced analytics.",
        _ => "Unknown subscription status."
    };

    public string AlarmSubscriptionDescription => AlarmSubscription switch
    {
        AlarmLevel.None => "No alarms",
        AlarmLevel.Basic => "Monitoring and alarm if values outside of static range",
        AlarmLevel.Premium => "Monitoring and alarm based on machine learning concepts",
        _ => "Unknown subscription status."
    };
}