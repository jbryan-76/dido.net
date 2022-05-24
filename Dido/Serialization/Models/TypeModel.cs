using System.Reflection;

namespace DidoNet
{
    /// <summary>
    /// Represents a serializable data model for a Type.
    /// </summary>
    internal class TypeModel
    {
        public TypeModel() { }

        public TypeModel(Type type)
        {
            Name = type.FullName ?? string.Empty;
            AssemblyName = type.Assembly.FullName ?? string.Empty;
            RuntimeVersion = type.Assembly.ImageRuntimeVersion;
        }

        public string Name { get; set; } = string.Empty;

        public string AssemblyName { get; set; } = string.Empty;

        public string RuntimeVersion { get; set; } = string.Empty;

        public Type? ToType(Environment env)
        {
            if (env.LoadedAssemblies.TryGetValue(AssemblyName, out var asm))
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
            return lhs is null ? rhs is null : lhs.Equals(new TypeModel(rhs));
        }

        public static bool operator !=(TypeModel lhs, Type rhs) => !(lhs == rhs);

        public override int GetHashCode()
        {
            return HashCode.Combine(Name.GetHashCode(), AssemblyName.GetHashCode());
        }
    }
}
