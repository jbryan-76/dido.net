namespace Anywhere.TestLibDependency
{
    /// <summary>
    /// A test class which serves as a dependency to Anywhere.TestLib located in another assembly.
    /// </summary>
    public class SampleDependencyClass
    {
        public string MyString { get; set; }

        public SampleDependencyModel MyModel { get; set; }

        public string GenerateCombinedString()
        {
            return MyString + MyModel.ToString();
        }
    }
}