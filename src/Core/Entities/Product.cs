using Nest;
using System;

namespace SomeBasicElasticApp.Core.Entities
{
    public class Product : IIdentifiableByNumber
    {
        public int Id { get; set; }

        public float Cost { get; set; }

        public string Name { get; set; }
    }
}
