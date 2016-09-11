namespace SomeBasicElasticApp.Core.Entities
{
    public class Customer : IIdentifiableByNumber
    {
        public int Id { get; set; }

        public string Firstname { get; set; }

        public string Lastname { get; set; }
    }
}
