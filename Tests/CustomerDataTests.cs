using System.IO;
using System.Xml.Linq;
using SomeBasicElasticApp.Core;
using NUnit.Framework;
using Order = SomeBasicElasticApp.Core.Entities.Order;
using System.Linq;
using SomeBasicElasticApp.Core.Entities;
using System;
using With;
using Elasticsearch.Net;
using Nest;

namespace SomeBasicElasticApp.Tests
{
    [TestFixture]
    public class CustomerDataTests
    {
        private ElasticClient _client;

        [Test]
        public void CanGetCustomerById()
        {
            var customer = _client.Get<Customer>(1).Source;

            Assert.IsNotNull(customer);
        }

        [Test]
        public void CustomerHasOrders()
        {
            var customerId = 1;
            var res = _client
                .Search<Order>(o =>
                    o.Query(q => q.Match(m => m.Field(f => f.Customer).Query(customerId.ToString())))
                        .Take(1));

            Assert.True(res.Hits.Single().Source.Customer == customerId);
        }

        [Test]
        public void ProductsArePartOfOrders()
        {
            var productId = 1;
            var res = _client
                .Search<OrderProduct>(o =>
                    o.Query(q => q.Match(m => m.Field(f => f.ProductId).Query(productId.ToString())))
                        .Take(1));
            Assert.True(res.Hits.Single().Source.ProductId == productId);
        }

        [Test]
        public void CanGetCustomerByFirstname()
        {
            var res = _client
                .Search<Customer>(o =>
                    o.Query(q => q.Match(m => m.Field(f => f.Firstname).Query("Steve")))
                        .Take(1));
            Assert.AreEqual(3, res.Total);
        }

        [Test]
        public void CanGetFirstNames()
        {
            var res = _client
                .Search<Customer>(s=>s.Aggregations(a=>
                    a.Terms("firstnames",t=>t.Field(f=>f.Firstname).ExecutionHint(TermsAggregationExecutionHint.Map))));
            var firstNames= res.Aggs.Terms("firstnames");
            // 
            Assert.That( firstNames.Buckets.Select(b=>b.Key).ToArray(),
                Is.EquivalentTo(new[] { "steve", "joe", "mike", "peter", "yuliana" }));
        }

        [Test]
        public void CanUpdateCustomer()
        {
            Customer customer = _client.Get<Customer>(1).Source;
            customer.Lastname += "_Updated";
            _client.Index(customer, s => s.Refresh());

            var c = _client.Get<Customer>(1).Source;
            Assert.That(c.Lastname, Is.EqualTo(customer.Lastname));
        }

        [Test]
        public void CanGetProductById()
        {
            var product = _client.Get<Product>(1).Source;

            Assert.IsNotNull(product);
        }

        [Test]
        public void OrderContainsProduct()
        {
            var orderId = 1;

            var res = _client
                .Search<OrderProduct>(o =>
                    o.Query(q => q.Match(m => m.Field(f => f.OrderId).Query(orderId.ToString())))
                        .Take(1));
            Assert.AreEqual(1, res.Total);
        }

        [Test]
        public void OrderHasACustomer()
        {
            Assert.IsTrue(_client.Get<Order>(1).Source.Customer > 0);
        }

        [SetUp]
        public void Setup()
        {
        }
        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            var node = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(
                node).DefaultIndex("customer-data-tests");
            _client = new ElasticClient(settings);
            var doc = XDocument.Load(Path.Combine("TestData", "TestData.xml"));
            var import = new XmlImport(doc, "http://tempuri.org/Database.xsd");
            import.Parse(new[] { typeof(Customer), typeof(Order), typeof(Product) },
                (type, obj) =>
                {
                    Switch.On(obj)
                        .Case((Customer c) => _client.Index(c, s => s.Refresh()))
                        .Case((Order o) => _client.Index(o, s => s.Refresh()))
                        .Case((Product p) => _client.Index(p, s => s.Refresh()))
                        .ElseFail();
                },
                onIgnore: (type, property) =>
                {
                    Console.WriteLine("ignoring property {1} on {0}", type.Name, property.PropertyType.Name);
                });

            int sequence = 0;
            import.ParseConnections("OrderProduct", "Product", "Order", (productId, orderId) =>
            {
                _client.Index(new OrderProduct { OrderId = productId, ProductId = orderId, Id = ++sequence });
            });
            import.ParseIntProperty("Order", "Customer", (orderId, customerId) =>
            {
                var order = _client.Get<Order>(orderId).Source;
                order.Customer = customerId;
                _client.Index(order);
            });
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _client.DeleteIndex("customer-data-tests");
        }
    }
}
