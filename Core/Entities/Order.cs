using Nest;
using System;
using System.Collections.Generic;

namespace SomeBasicElasticApp.Core.Entities
{
    public class Order : IIdentifiableByNumber
    {
        public int Id { get; set; }

        public int Customer { get; set; }

        public DateTime OrderDate { get; set; }
    }
}
