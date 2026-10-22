using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions; 
using Npgsql; 
using WebAppRabbitMQ.Models;

namespace WebAppRabbitMQ.Tests;

public class FakePublisher : IEventPublisher
{
    public ConcurrentQueue<TaskCompletedMessage> Messages { get; } = new();
    public void Publish(TaskCompletedMessage message) => Messages.Enqueue(message);
}
 
public class TestApiFactory : WebApplicationFactory<Program>
{
    public FakePublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var cs = Environment.GetEnvironmentVariable("TEST_DB");
            if (string.IsNullOrWhiteSpace(cs))
            {
                var configured = cfg.Build().GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection not found");
                cs = new NpgsqlConnectionStringBuilder(configured) { Database = "rabbitmq_tests" }.ConnectionString;
            }

            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = cs });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Publisher);
        });
    }
}

[TestClass]
public class CompleteTaskIntegrationTest
{
    [TestMethod]
    public async Task CreateTask_ThenComplete_PublishesTaskCompletedMessage()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new { title = "Buy milk", priority = "High" });
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.IsFalse(created.GetProperty("isCompleted").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, created.GetProperty("completedAt").ValueKind); 

        // 2. Завершаем
        var completeResponse = await client.PutAsync($"/tasks/{id}/complete", null);
        Assert.AreEqual(HttpStatusCode.OK, completeResponse.StatusCode);

        var completed = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.IsTrue(completed.GetProperty("isCompleted").GetBoolean());
        Assert.AreNotEqual(JsonValueKind.Null, completed.GetProperty("completedAt").ValueKind);

        var messages = factory.Publisher.Messages.Where(m => m.TaskId == id).ToList();
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual("Buy milk", messages[0].Title);
        Assert.AreEqual(Priority.High, messages[0].Priority);
        Assert.IsTrue(DateTimeOffset.UtcNow - messages[0].CompletedAt < TimeSpan.FromMinutes(1));

        await client.DeleteAsync($"/tasks/{id}");
    }
}