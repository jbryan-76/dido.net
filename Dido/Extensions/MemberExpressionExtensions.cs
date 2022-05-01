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
        public static bool GetConstantValue(this MemberExpression exp, out Type type, out object value)
        {
            if (!(exp.Expression is ConstantExpression))
            {
                type = null;
                value = null;
                return false;
            }
            object container = ((ConstantExpression)exp.Expression).Value;
            switch (exp.Member)
            {
                case FieldInfo info:
                    type = ((FieldInfo)exp.Member).FieldType;
                    value = ((FieldInfo)exp.Member).GetValue(container);
                    break;
                case PropertyInfo info:
                    type = ((PropertyInfo)exp.Member).PropertyType;
                    value = ((PropertyInfo)exp.Member).GetValue(container);
                    break;
                default:
                    throw new NotImplementedException($"Could not get constant value from member expression: {exp.Member.GetType()} is not supported.");
            }
            return true;
        }
    }
}
