namespace Common.Configuration
{
    public interface IFieldSubstitution
    {
        string Field { get; set; }

        string Value { get; set; }
    }
}
