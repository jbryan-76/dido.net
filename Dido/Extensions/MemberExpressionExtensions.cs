using System.Linq.Expressions;
using System.Reflection;

namespace DidoNet
{
    public static class MemberExpressionExtensions
    {
        /// <summary>
        /// If the containing object of the member expression is a constant,
        /// get its value and type and return true, else return false.
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static bool GetConstantValue(this MemberExpression exp, out Type? type, out object? value)
        {
            if (exp.Expression is not ConstantExpression)
            {
                type = null;
                value = null;
                return false;
            }
            var container = ((ConstantExpression)exp.Expression).Value;
            switch (exp.Member)
            {
                case FieldInfo info:
                    type = info.FieldType;
                    value = info.GetValue(container);
                    break;
                case PropertyInfo info:
                    type = info.PropertyType;
                    value = info.GetValue(container);
                    break;
                default:
                    throw new NotImplementedException($"Could not get constant value from member expression: {exp.Member.GetType()} is not supported.");
            }
            return true;
        }
    }
}
