namespace Common.Configuration
{
    public interface IFieldReplacement
    {
        string Field { get; set; }

        string Pattern { get; set; }

        string Replacement { get; set; }
    }
}
