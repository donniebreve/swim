using Newtonsoft.Json;

namespace Common.Configuration
{
    public interface IConnection
    {
        string Account { get; set; }

        string Project { get; set; }

        string AccessToken { get; set; }

        bool UseIntegratedAuth { get; set; }
    }
}
