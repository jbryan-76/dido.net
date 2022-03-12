using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnywhereNET.TestLibDependency
{
    /// <summary>
    /// A POCO model used for testing proper serialization and invokation of
    /// dynamically loaded assemblies.
    /// </summary>
    public class SampleDependencyModel
    {
        public int MyInt { get; set; }
        public bool MyBool { get; set; }
        public DateTimeOffset MyDateTimeOffset { get; set; }

        public string MyFormatDate()
        {
            return MyDateTimeOffset.ToString("O");
        }

        public override string ToString()
        {
            return $"{MyInt} {MyBool} {MyFormatDate()}";
        }
    }
}
