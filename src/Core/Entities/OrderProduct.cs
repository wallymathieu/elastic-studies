using Nest;
using System;

namespace SomeBasicElasticApp.Core
{
    public class OrderProduct:IIdentifiableByNumber
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
    }
}

