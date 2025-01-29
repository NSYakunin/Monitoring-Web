namespace web_test
{
    public class Person
    {
        public string Name { get; }
        public int Age { get; }
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }
        public override string ToString() => $"Person {Name} ({Age} лет)";
    }
}
