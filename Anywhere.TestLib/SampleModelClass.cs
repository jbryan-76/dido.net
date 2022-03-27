namespace AnywhereNET.TestLib
{
    public class SampleModelClass
    {
        public int MyInt { get; set; }

        public string MyString { get; set; }

        public int MySetIntMethod(int val)
        {
            MyInt = val;
            return MyInt;
        }
    }
}