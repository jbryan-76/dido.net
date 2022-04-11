using System.Reflection;

namespace AnywhereNET
{
    internal class MemberInfoModel
    {
        public string Name { get; set; }
        public TypeModel DeclaringType { get; set; }

        public MemberInfoModel() { }
        public MemberInfoModel(MemberInfo info)
        {
            Name = info.Name;
            DeclaringType = new TypeModel(info.DeclaringType);
        }

        public MemberInfo ToInfo(Environment env)
        {
            var declaringType = DeclaringType.ToType(env);
            return declaringType.GetMember(Name, Constants.AllMembers).FirstOrDefault();
        }
    }
}
