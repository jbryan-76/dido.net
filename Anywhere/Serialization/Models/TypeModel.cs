using System.Reflection;

namespace AnywhereNET
{
    internal class TypeModel
    {
        public TypeModel() { }
        public TypeModel(Type type)
        {
            Name = type.FullName;
            AssemblyName = type.Assembly.FullName;
            RuntimeVersion = type.Assembly.ImageRuntimeVersion;
        }
        public string Name { get; set; }
        public string AssemblyName { get; set; }
        public string RuntimeVersion { get; set; }

        public Type ToType(Environment env)
        {
            if (env.LoadedAssemblies.TryGetValue(AssemblyName, out Assembly asm))
            {
                return asm.GetType(Name);
            }
            throw new FileNotFoundException($"Could not resolve assembly '{AssemblyName}' from current Environment.", AssemblyName);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }
            else if (Object.ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != typeof(TypeModel))
            {
                return false;
            }
            else
            {
                var t = (TypeModel)obj;
                return Name == t.Name && AssemblyName == t.AssemblyName && RuntimeVersion == t.RuntimeVersion;
            }
        }

        public static bool operator ==(TypeModel lhs, Type rhs)
        {
            if (lhs is null)
            {
                return rhs is null ? true : false;
            }
            else
            {
                return lhs.Equals(new TypeModel(rhs));
            }
        }

        public static bool operator !=(TypeModel lhs, Type rhs) => !(lhs == rhs);
    }
}
