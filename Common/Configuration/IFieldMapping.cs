namespace Common.Configuration
{
    public interface IFieldMapping
    {
        string SourceField { get; set; }

        string TargetField { get; set; }
    }
}
