using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using grefurBackend.Types;
using Microsoft.EntityFrameworkCore;


namespace grefurBackend.Models;

[Owned]
public class UsageStats
{
    public long Total { get; set; }
    public long LastYear { get; set; }
    public long LastQuarter { get; set; }
    public long LastMonth { get; set; }
    public long ThisMonth { get; set; }
}
[Owned]
public class NotificationUsage
{
    public UsageStats Sms { get; set; } = new();
    public UsageStats Email { get; set; } = new();
}

[CustomerServicesMetaData("Logging Service", "Level of logging of values in the Grefur Datalake")]
public enum LogSubscriptionLevel
{
    [Display(Name = "None", Description = "No logging allowed. Real-time monitoring only.")]
    None = 0,

    [Display(Name = "Normal", Description = "Standard logging with 30 days retention.")]
    Normal = 1,

    [Display(Name = "Premium", Description = "High-frequency logging with 1 year retention and analytics.")]
    Premium = 2
}

[CustomerServicesMetaData("Alarm Algorithm", "Level of alarms and monitoring intelligence")]
public enum AlarmLevel
{
    [Display(Name = "None", Description = "No active alarms.")]
    None = 0,

    [Display(Name = "Basic", Description = "Monitoring and alarm if values outside of static range.")]
    Basic = 1,

    [Display(Name = "Premium", Description = "Monitoring and alarm based on Grefur machine learning concepts.")]
    Premium = 2
}

[CustomerServicesMetaData("Notification Level", "Level of user interaction the system will bring")]
public enum NotificationTypes
{
    [Display(Name = "None", Description = "No external notifications.")]
    None = 0,

    [Display(Name = "SMS", Description = "Receive alerts via SMS.")]
    SMS = 1,

    [Display(Name = "Email", Description = "Receive alerts via Email.")]
    EMAIL = 2,

    [Display(Name = "SMS & Email", Description = "Receive alerts on both SMS and Email.")]
    SMS_EMAIL = 3,

    /*[Display(Name = "Grefur Full", Description = "Full suite: Grefur App, SMS, and Email.")]
    GREFUR_SMS_EMAIL = 4,*/
}


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Property)]
public class MetaDataAttribute : Attribute
{
    public string SingleName { get; }
    public string VerboseName { get; }
    public string Description { get; }

    public MetaDataAttribute(string singleName, string verboseName, string description)
    {
        SingleName = singleName;
        VerboseName = verboseName;
        Description = description;
    }
}



public record SubscriptionInfo(LogSubscriptionLevel Level, string Description);

[MetaData("Customer", "Customers", "Registered customer in the Grefur Ecosystem")]
public class GrefurCustomer
{
    [Key]
    public string CustomerId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationNumber { get; set; } = string.Empty;

    public string EmailOfOrganization { get; set; } = string.Empty;

    // Status
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    //public List<string> RegisteredDevices { get; set; } = new();

    // Default subscriptions
    public LogSubscriptionLevel LogSubscription { get; set; } = LogSubscriptionLevel.None;
    public AlarmLevel AlarmSubscription { get; set; } = AlarmLevel.None;
    public NotificationTypes NotificationSubscription { get; set; } = NotificationTypes.None;

    // --- Usage Metrics ---

    // Logged points (Data for total, last year, quarter, last month, this month)
    public UsageStats LoggedPointsUsage { get; set; } = new();

    // Notifications (Detailed split between SMS and Email)
    public NotificationUsage NotificationUsage { get; set; } = new();

    /// <summary>
    /// Dynamically checks if the customer has any active subscription or notification type.
    /// </summary>
    public bool IsActiveSubscriber()
    {
        var Properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var Prop in Properties)
        {
            if (Prop.Name.EndsWith("Subscription") || Prop.Name.EndsWith("Types"))
            {
                var Value = Prop.GetValue(this);
                if (Value == null) continue;

                if (Value.ToString() != "None" && (int)Value != 0)
                {
                    return true;
                }
            }
        }
        return false;
    }


    public bool Invoiced()
    {
        try
        {
            // 1. Roter logging-statistikk
            RotateStats(LoggedPointsUsage);

            // 2. Roter varslings-statistikk (SMS og Email)
            RotateStats(NotificationUsage.Sms);
            RotateStats(NotificationUsage.Email);

            return true;
        }
        catch (Exception)
        {
            // Logikk for feilhåndtering kan legges her hvis ønskelig
            return false;
        }
    }

    private void RotateStats(UsageStats Stats)
    {
        if (Stats == null) return;

        // Flytt data fra denne måneden til forrige måned
        Stats.LastMonth = Stats.ThisMonth;

        // Nullstill denne måneden for ny periode
        Stats.ThisMonth = 0;

        // Her kan man også legge til logikk for å akkumulere 
        // LastQuarter eller LastYear hvis datoen tilsier det
    }

    /// Increments the logging usage for the customer.
    public void AddLogPoint(int Count = 1)
    {
        LoggedPointsUsage.ThisMonth += Count;
        LoggedPointsUsage.Total += Count;
    }

    /// Increments the notification usage based on type (SMS or Email).
    public void AddAlert(NotificationTypes Type, int Count = 1)
    {
        if (Type == NotificationTypes.SMS || Type == NotificationTypes.SMS_EMAIL)
        {
            NotificationUsage.Sms.ThisMonth += Count;
            NotificationUsage.Sms.Total += Count;
        }

        if (Type == NotificationTypes.EMAIL || Type == NotificationTypes.SMS_EMAIL)
        {
            NotificationUsage.Email.ThisMonth += Count;
            NotificationUsage.Email.Total += Count;
        }
    }




    // Helper properties (updated to use the correct Enum name)
    public string LogSubscriptionDescription => LogSubscription switch
    {
        LogSubscriptionLevel.None => "No logging allowed. Real-time monitoring only.",
        LogSubscriptionLevel.Normal => "Standard logging with 30 days retention.",
        LogSubscriptionLevel.Premium => "High-frequency logging with 1 year retention and advanced analytics.",
        _ => "Unknown subscription status."
    };

    public string AlarmSubscriptionDescription => AlarmSubscription switch
    {
        AlarmLevel.None => "No alarms",
        AlarmLevel.Basic => "Monitoring and alarm if values outside of static range",
        AlarmLevel.Premium => "Monitoring and alarm based on machine learning concepts",
        _ => "Unknown subscription status."
    };

    public string NotificationSubscriptionDescription => NotificationSubscription switch
    {
        NotificationTypes.None => "No notifications",
        NotificationTypes.SMS => "Quick feedback directly on SMS",
        NotificationTypes.EMAIL => "Safe feedback on email",
        NotificationTypes.SMS_EMAIL => "Both quick and safe notification",
        _ => "Unknown."
    };
}