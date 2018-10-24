using System.IO;
using System.Xml.Linq;
using SomeBasicElasticApp.Core;
using Order = SomeBasicElasticApp.Core.Entities.Order;
using System.Linq;
using SomeBasicElasticApp.Core.Entities;
using System;
using System.Threading;
using Elasticsearch.Net;
using Nest;
using Xunit;

namespace SomeBasicElasticApp.Tests
{
    public class CustomerDataTests
    {
        private static readonly ElasticClient Client;
        private const string OrdersIndex = "customer-data-tests-orders";
        private const string OrderProductsIndex = "customer-data-tests-order-products";
        private const string ProductsIndex = "customer-data-tests-products";
        private const string CustomersIndex = "customer-data-tests-customers";

        [Fact]
        public void CanGetCustomerById()
        {
            Assert.NotNull(Client.Get<Customer>(1, s=>s.Index(CustomersIndex)).Source);
        }

        [Fact]
        public void CustomerHasOrders()
        {
            var customerId = 1;
            var res = Client
                .Search<Order>(o =>
                    o.Query(q => q.Match(m => m.Field(f => f.Customer).Query(customerId.ToString())))
                        .Take(1));

            Assert.True(res.Hits.Single().Source.Customer == customerId);
        }

        [Fact]
        public void ProductsArePartOfOrders()
        {
            var productId = 1;
            var res = Client
                .Search<OrderProduct>(o =>o
                        .Index(OrderProductsIndex)
                        .Query(q => q.Match(m => m.Field(f => f.ProductId).Query(productId.ToString())))
                        .Take(1));
            Assert.True(res.Hits.Single().Source.ProductId == productId);
        }

        [Fact]
        public void CanGetCustomerByFirstname()
        {
            var res = Client
                .Search<Customer>(o =>o
                    .Index(CustomersIndex)
                    .Query(q => q.Match(m => m.Field(f => f.Firstname).Query("Steve")))
                    .Take(1));
            Assert.Equal(3, res.Total);
        }

        [Fact(Skip = "Needs to be ported")]
        public void CanGetFirstNames()
        {
            var res = Client
                .Search<Customer>(s => s
                    .Index(CustomersIndex)
                    .Aggregations(a =>
                    a.Terms("firstnames",
                        t => t.Field(f => f.Firstname).ExecutionHint(TermsAggregationExecutionHint.Map))));
            var firstNames = res.Aggregations.Terms("firstnames");
            // 
            Assert.Equal(new[] {"steve", "joe", "mike", "peter", "yuliana"},
                firstNames.Buckets.Select(b => b.Key).ToArray());
        }

        [Fact]
        public void CanUpdateCustomer()
        {
            Customer customer = Client.Get<Customer>(1, s=>s.Index(CustomersIndex)).Source;
            customer.Lastname += "_Updated";
            Client.Index(customer, s => s.Index(CustomersIndex).Refresh(Refresh.WaitFor));

            var c = Client.Get<Customer>(1, s=>s.Index(CustomersIndex)).Source;
            Assert.Equal(customer.Lastname, c.Lastname);
        }

        [Fact]
        public void CanGetProductById()
        {
            var product = Client.Get<Product>(1, s=>s.Index(ProductsIndex)).Source;

            Assert.NotNull(product);
        }

        [Fact]
        public void OrderContainsProduct()
        {
            var orderId = 1;

            var res = Client
                .Search<OrderProduct>(o =>o
                    .Index(OrderProductsIndex)
                    .Query(q => q.Match(m => m.Field(f => f.OrderId).Query(orderId.ToString())))
                    .Take(1));
            Assert.Equal(1, res.Total);
        }

        [Fact]
        public void OrderHasACustomer()
        {
            Assert.True(Client.Get<Order>(1,s=>s.Index(OrdersIndex)).Source.Customer > 0);
        }

        static CustomerDataTests()
        {
            void AssertCreatedOrUpdated(IIndexResponse resp)
            {
                if (resp.Result != Result.Created && resp.Result != Result.Updated)
                    throw new Exception(
$@"Expected result to be created or updated: 
Result: {resp.Result}
DebugInformation: {resp.DebugInformation}
ServerError: {resp.ServerError?.Error}");
            }

            var node = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(
                node).DefaultIndex("customer-data-tests");
            Client = new ElasticClient(settings);
            Client.DeleteIndex("customer-data-tests*");
            Thread.Sleep(1000);
            Client.CreateIndex(CustomersIndex);
            Client.CreateIndex(OrdersIndex);
            Client.CreateIndex(ProductsIndex);
            var doc = XDocument.Load(Path.Combine("TestData", "TestData.xml"));
            var import = new XmlImport(doc, "http://tempuri.org/Database.xsd");
            import.Parse(new[] {typeof(Customer), typeof(Order), typeof(Product)},
                (type, obj) =>
                {
                    switch (obj)
                    {
                        case Customer c:
                        {
                            AssertCreatedOrUpdated(Client.Index(c, s => s.Refresh(Refresh.WaitFor)
                                .Index(CustomersIndex).Id(c.Id)));
                            break;
                        }
                        case Order o:
                        {
                            AssertCreatedOrUpdated(Client.Index(o, s => s.Refresh(Refresh.WaitFor)
                                .Index(OrdersIndex).Id(o.Id)));
                            break;
                        }
                        case Product p:
                        {
                            AssertCreatedOrUpdated(Client.Index(p, s => s.Refresh(Refresh.WaitFor)
                                .Index(ProductsIndex).Id(p.Id)));
                            break;
                        }
                        default: throw new Exception("!");
                    }
                },
                (type, property) => Console.WriteLine("ignoring property {1} on {0}", type.Name, property.PropertyType.Name));
            Thread.Sleep(1000);
            int sequence = 0;
            import.ParseConnections("OrderProduct", "Product", "Order",
                (productId, orderId) =>
                {
                    Client.Index(new OrderProduct {OrderId = productId, ProductId = orderId, Id = ++sequence},
                        s=>s.Refresh(Refresh.False).Index(OrderProductsIndex));
                });
            import.ParseIntProperty("Order", "Customer", (orderId, customerId) =>
            {
                var orderResponse = Client.Get<Order>(orderId,s=>s.Index(OrdersIndex));
                if (!orderResponse.Found)
                {
                    throw new Exception($"Could not find order {orderId}");
                }
                var order = orderResponse.Source;
                order.Customer = customerId;
                Client.Index(order, s=>s.Refresh(Refresh.False));
            });
        }
    }
}