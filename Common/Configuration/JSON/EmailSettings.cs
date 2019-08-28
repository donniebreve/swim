using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    public class EmailSettings : IEmailSettings
    {
        [JsonProperty(PropertyName = "smtp-server", Required = Required.Always)]
        public string SmtpServer { get; set; }

        [JsonProperty(PropertyName = "use-ssl", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool UseSsl { get; set; }

        [JsonProperty(PropertyName = "port", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(25)]
        public int Port { get; set; }

        [JsonProperty(PropertyName = "from-address", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue("wimigrator@example.com")]
        public string FromAddress { get; set; }

        [JsonProperty(PropertyName = "recipient-addresses", DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<string> RecipientAddresses { get; set; }

        [JsonProperty(PropertyName = "user-name", Required = Required.DisallowNull)]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "password", Required = Required.DisallowNull)]
        public string Password { get; set; }

        /// <summary>
        /// Determines if Newtonsoft.Json should serialize the field. 
        /// </summary>
        /// <returns>False</returns>
        public bool ShouldSerializePassword()
        {
            return false;
        }
    }
}
