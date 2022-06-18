using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DidoNet
{
    public static class TypeExtensions
    {
        /// <summary>
        /// A list of the basic built-in scalar types (reference, value, and struct types) supported as serializable properties.
        /// </summary>
        public static readonly IEnumerable<Type> BasicScalarTypes = new List<Type>
        {
            // reference types
            typeof(string),

            // value types
            typeof(bool),
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(char),

            // structs
            typeof(DateTimeOffset),
            typeof(DateTime),
            typeof(Guid),
        };

        /// <summary>
        /// Indicates whether the given type is a compiler generated type, often used to 
        /// capture local variables in closures.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsCompilerGeneratedType(this Type type)
        {
            return type.FullName?.Contains("<>") ?? false;
        }

        /// <summary>
        /// Returns true if the member is ignored during serialization (ie has the NonSerialized attribute)
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool IsIgnored(this MemberInfo prop)
        {
            return prop.GetCustomAttribute<NonSerializedAttribute>() != null;
        }

        /// <summary>
        /// Returns true if the member is declared by the provided type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool IsDeclaredBy<T>(this MemberInfo prop)
        {
            return typeof(T).Equals(prop.DeclaringType);
        }

        /// <summary>
        /// Returns true if the member is serializable.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static bool IsSerializable(this MemberInfo member, List<string>? errors = null)
        {
            if (member.IsIgnored())
            {
                return false;
            }

            var errs = new List<string>();
            switch (member)
            {
                case PropertyInfo info:
                    if (!IsSerializable(info.PropertyType, errs))
                    {
                        errors?.Add($"Property {member.Name}: {string.Join("", errs)}");
                        return false;
                    }
                    break;
                case FieldInfo info:
                    if (!IsSerializable(info.FieldType, errs))
                    {
                        errors?.Add($"Field {member.Name}: {string.Join("", errs)}");
                        return false;
                    }
                    break;
                default:
                    throw new NotSerializableException($"Only Property and Field members are serializable.");
            }
            return true;
        }

        /// <summary>
        /// Throws an exception if the type is not serializable.
        /// </summary>
        /// <param name="type"></param>
        public static void AssertIsSerializable(this Type type)
        {
            var errors = new List<string>();
            if (!type.IsSerializable(errors))
            {
                throw new AggregateException(errors.Select(e => new NotSerializableException(e)));
            }
        }

        /// <summary>
        /// Returns true if the type is serializable.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static bool IsSerializable(this Type type, List<string>? errors = null)
        {
            var error = $"is not serializable. Use [NonSerialized] on properties if they should not be serialized";

            // any scalar type is serializable
            if (IsScalar(type))
            {
                return true;
            }

            // any array of a serializable type is serializable
            if (IsArray(type))
            {
                var arrayType = type.GetElementType()!;
                var errs = new List<string>();
                if (!IsSerializable(arrayType, errs))
                {
                    errors?.Add($"Type {arrayType.Name}[] {error}. [Array type {arrayType.Name} is not serializable]");
                    errors?.AddRange(errs);
                    return false;
                }
                return true;
            }

            // any dictionary with a serializable key type and a serializable value type is serializable
            if (IsDictionary(type))
            {
                // dynamic types are not supported
                if (type.GenericTypeArguments.Count() == 0)
                {
                    errors?.Add($"Type {type.Name} {error}.");
                    return false;
                }

                var keyType = type.GenericTypeArguments.First();
                var valueType = type.GenericTypeArguments.Last();
                var errs = new List<string>();
                if (!IsSerializable(keyType, errs))
                {
                    errors?.Add($"Type {type.Name}<{keyType.Name},{valueType.Name}> {error}. [Dictionary key type {keyType.Name} is not serializable]");
                    return false;
                }
                if (!IsSerializable(valueType, errs))
                {
                    errors?.Add($"Type {type.Name}<{keyType.Name},{valueType.Name}> {error}. [Dictionary value type {valueType.Name} is not serializable]");
                    errors?.AddRange(errs);
                    return false;
                }
                return true;
            }

            // any enumerable of a serializable type is serializable
            if (IsEnumerable(type))
            {
                var argType = type.GenericTypeArguments.First();
                if (!IsSerializable(argType))
                {
                    errors?.Add($"Type {type.Name}<{argType.Name}> {error}. [Generic type argument {argType.Name} is not serializable]");
                    return false;
                }
                return true;
            }

            // any complex type (ie class) composed ONLY of serializable or ignored properties is serializable
            if (type.IsClass)
            {
                if (!type.GetProperties()
                    .Where(p => !p.IsIgnored())
                    .All(p => IsSerializable(p, errors)))
                {
                    errors?.Add($"Type {type.Name} {error}. [One or more properties are not serializable]");
                    return false;
                }
                return true;
            }

            // otherwise the type is not serializable
            errors?.Add($"Type {type.Name} {error}.");

            return false;
        }

        /// <summary>
        /// Returns true if the given type is one of the basic supported value, reference, or struct types.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsBasic(this Type type)
        {
            return BasicScalarTypes.Contains(type);
        }

        /// <summary>
        /// Returns true if the given type is an enum.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEnum(this Type type)
        {
            return type.IsEnum;
        }

        /// <summary>
        /// Returns true if the given type is a nullable basic or enum type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType
                && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>))
                && type.GenericTypeArguments.All(t => IsBasic(t) || IsEnum(t));
        }

        /// <summary>
        /// Returns true if the given type is a supported scalar (non-compound, non-custom, non-enumerable) type, ie a basic, enum, or nullable type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsScalar(this Type type)
        {
            return IsBasic(type)
                || IsEnum(type)
                || IsNullable(type);
        }

        /// <summary>
        /// Returns true if the given type is an array.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsArray(this Type type)
        {
            return !IsScalar(type) && typeof(Array).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns true if the given type is enumerable.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEnumerable(this Type type)
        {
            return !IsScalar(type) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns true if the given type is a dictionary.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsDictionary(this Type type)
        {
            return !IsScalar(type) && typeof(IDictionary).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns true if the given type is a non-scalar, non-enumerable, non-dictionary custom (class) type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsCustomType(this Type type)
        {
            return type.IsClass
                && !IsScalar(type)
                && !IsEnumerable(type)
                && !IsDictionary(type);
        }

        /// <summary>
        /// Gets and returns the indicated custom attribute (if it exists) from a Type, else null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static T? GetCustomAttribute<T>(this Type type) where T : Attribute
        {
            return type.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T;
        }
    }
}
