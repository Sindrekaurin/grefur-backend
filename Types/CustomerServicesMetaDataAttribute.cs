[AttributeUsage(AttributeTargets.Enum)]
public class CustomerServicesMetaDataAttribute : Attribute
{
    public string Title { get; }
    public string Description { get; }
    public CustomerServicesMetaDataAttribute(string title, string description)
    {
        Title = title;
        Description = description;
    }
}