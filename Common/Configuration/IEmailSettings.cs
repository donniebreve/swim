using System.Collections.Generic;
using System.ComponentModel;

namespace Common.Configuration
{
    public interface IEmailSettings
    {
        string SmtpServer { get; set; }

        [DefaultValue(false)]
        bool UseSsl { get; set; }

        [DefaultValue(25)]
        int Port { get; set; }

        [DefaultValue("wimigrator@example.com")]
        string FromAddress { get; set; }

        List<string> RecipientAddresses { get; set; }

        string UserName { get; set; }

        string Password { get; set; }
    }
}
