using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Serialization
{
    public interface ISerializer
    {
        string Serialize(object o);

        T Deserialize<T>(string s);
    }
}
