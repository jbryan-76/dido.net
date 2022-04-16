using System.Reflection;

namespace DidoNet
{
    internal class MethodInfoModel : MemberInfoModel
    {
        public TypeModel ReturnType { get; set; }
        public MethodInfoModel() { }
        public MethodInfoModel(MethodInfo info) : base(info)
        {
            ReturnType = new TypeModel(info.ReturnType);
        }

        public new MethodInfo ToInfo(Environment env)
        {
            var declaringType = DeclaringType.ToType(env);
            return declaringType.GetMethod(Name, Constants.AllMembers);
        }
    }
}
