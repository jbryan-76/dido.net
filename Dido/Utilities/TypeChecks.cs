using System;
using System.Collections.Generic;

namespace DidoNet
{
    public static class TypeChecks
    {
        /// <summary>
        /// Confirms the provided type(s) are serializable and throws an exception for 
        /// non-serializable and non-ignored properties or fields.
        /// </summary>
        public static void CheckSerializableProperties(params Type[] types)
        {
            CheckSerializableProperties((IEnumerable<Type>)types);
        }

        /// <summary>
        /// Confirms the provided type(s) are serializable and throws an exception for 
        /// non-serializable and non-ignored properties or fields.
        /// </summary>
        public static void CheckSerializableProperties(IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                var errors = new List<string>();
                if (!type.IsSerializable(errors))
                {
                    throw new NotSerializableException($"Type {type.Name} is not serializable. {string.Join("; ", errors)}");
                }
            }
        }
    }
}
